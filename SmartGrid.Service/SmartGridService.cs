using System;
using System.Collections.Generic;
using System.ServiceModel;
using SmartGrid.Common;
using System.IO;

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

        public void StartSession(string meta)
        {
            if (string.IsNullOrWhiteSpace(meta))
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Meta informacije ne mogu biti prazne."));

            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();

            // Kreira fajl ako ne postoji
            if (!File.Exists(_filePath))
            {
                using (var writer = File.CreateText(_filePath)) { }
            }
        }

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

            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }


        public void EndSession()
        {
            // 7. Zadatak: Zavrsen prenos
            if (_isStreaming)
            {
                Console.WriteLine("Završen prenos");
                _isStreaming = false;
            }

            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");
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
}
