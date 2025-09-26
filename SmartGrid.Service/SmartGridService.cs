using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.ServiceModel;
using SmartGrid.Common;

namespace SmartGrid.Service
{
    [ServiceBehavior(
        IncludeExceptionDetailInFaults = true,
        InstanceContextMode = InstanceContextMode.Single,
        ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class SmartGridService : ISmartGridService
    {
        //3. Zadatak: WCF servis, operacije i validacija podataka
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();
        private readonly string _filePath = "measurements_session.csv";
        // 7. Zadatak: sekvencijalni streaming - flag za prenos u toku
        private bool _isStreaming = false;

        //8.
        // Pragovi
        private readonly double fftThreshold;
        private readonly double fThreshold;

        // Prosjecna frekvencija po sesiji
        private double _avgFrequency = 0;
        private int _count = 0;

        public SmartGridService()
        {
            fftThreshold = double.Parse(ConfigurationManager.AppSettings["FFT_threshold"]);
            fThreshold = double.Parse(ConfigurationManager.AppSettings["F_threshold"]);

            // Pretplate na dogadjaje
            OnTransferStarted += (s, e) => Console.WriteLine("[DOGAĐAJ] Prenos je zapocet.");
            OnSampleReceived += (s, e) =>
                Console.WriteLine($"[DOGAĐAJ] Primljen sample @ {e.Sample.Timestamp}, F={e.Sample.Frequency}");
            OnTransferCompleted += (s, e) => Console.WriteLine("[DOGAĐAJ] Prenos zavrsen.");
            OnWarningRaised += (s, e) => Console.WriteLine($"[UPOZORENJE] {e.Message}");
        }

        // Dogadjaji
        public event EventHandler<EventArgs> OnTransferStarted;
        public event EventHandler<SampleEventArgs> OnSampleReceived;
        public event EventHandler<EventArgs> OnTransferCompleted;
        public event EventHandler<WarningEventArgs> OnWarningRaised;

        public void StartSession(string meta)
        {
            if (string.IsNullOrWhiteSpace(meta))
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Meta informacije ne mogu biti prazne."));

            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();

            // Reset proseka
            _avgFrequency = 0;
            _count = 0;

            // Kreiranje fajla ako ne postoji
            if (!File.Exists(_filePath))
            {
                using (var writer = File.CreateText(_filePath)) { }
            }

            OnTransferStarted?.Invoke(this, EventArgs.Empty);
        }
        //do ovoga 8
        public void PushSample(SmartGridSample sample)
        {
            if (sample == null)
            {
                LogReject(null, "Sample je null");
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Uzorak ne moze biti null."));
            }

            if (sample.Frequency <= 0)
            {
                LogReject(sample, $"Nevalidna frekvencija: {sample.Frequency}");
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Frekvencija mora biti veca od 0."));
            }

            if (double.IsNaN(sample.FFT1) || double.IsNaN(sample.FFT2) ||
                double.IsNaN(sample.FFT3) || double.IsNaN(sample.FFT4))
            {
                LogReject(sample, "FFT vrednosti nisu validne.");
                throw new FaultException<ValidationFault>(
                    new ValidationFault("FFT vrednosti moraju biti validni brojevi."));
            }

            // 7. Zadatak: Prikaz statusa prenosa
            if (!_isStreaming)
            {
                Console.WriteLine("Prenos u toku...");
                _isStreaming = true;
            }

            _samples.Add(sample);

            try
            {
                using (var writer = new SampleWriter(_filePath))
                {
                    writer.WriteSample(sample);
                }
            }
            catch (Exception ex)
            {
                LogReject(sample, $"Greska pri snimanju: {ex.Message}");
                throw new FaultException<ValidationFault>(
                    new ValidationFault($"Greska pri snimanju uzorka: {ex.Message}"));
            }
            //8.
            OnSampleReceived?.Invoke(this, new SampleEventArgs(sample));

            // Provjera FFT pragova
            if (sample.FFT1 > fftThreshold || sample.FFT2 > fftThreshold ||
                sample.FFT3 > fftThreshold || sample.FFT4 > fftThreshold)
            {
                OnWarningRaised?.Invoke(this,
                    new WarningEventArgs($"Prekoracen FFT prag ({fftThreshold})"));
            }

            // Azuriranje prosjecne frekvencije
            _count++;
            _avgFrequency = ((_avgFrequency * (_count - 1)) + sample.Frequency) / _count;

            if (Math.Abs(sample.Frequency - _avgFrequency) > _avgFrequency * 0.25)
            {
                OnWarningRaised?.Invoke(this,
                    new WarningEventArgs(
                        $"Frekvencija {sample.Frequency} odstupa vise od ±25% od tekućeg prosjeka {_avgFrequency:F2}"));
            }

            // Provjera prema fiksnom pragu
            if (Math.Abs(sample.Frequency - fThreshold) > fThreshold * 0.25)
            {
                OnWarningRaised?.Invoke(this,
                    new WarningEventArgs(
                        $"Frekvencija van opsega ±25% od {fThreshold}Hz. Izmjereno: {sample.Frequency}"));
            }

            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }


        public void EndSession()
        {
            // 7. Zadatak: Zavrsen prenos
            if (_isStreaming)
            {
                Console.WriteLine("Zavrsen prenos");
                _isStreaming = false;
            }

            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");

            OnTransferCompleted?.Invoke(this, EventArgs.Empty);
        }

        //6. Zadatak: Snimanje i organizacija fajlova na serveru -> reject.csv
        private void LogReject(SmartGridSample sample, string reason)
        {
            using (var writer = new StreamWriter("rejects.csv", append: true))
            {
                string line = sample != null
                    ? $"{DateTime.Now}, {reason}, {sample.Timestamp},{sample.FFT1},{sample.FFT2},{sample.FFT3},{sample.FFT4},{sample.PowerUsage},{sample.Frequency}"
                    : $"{DateTime.Now}, {reason}, NULL sample";
                writer.WriteLine(line);
            }
        }
    }
    //8
    public class SampleEventArgs : EventArgs
    {
        public SmartGridSample Sample { get; }
        public SampleEventArgs(SmartGridSample sample) => Sample = sample;
    }

    public class WarningEventArgs : EventArgs
    {
        public string Message { get; }
        public WarningEventArgs(string message) => Message = message;
    }
}
