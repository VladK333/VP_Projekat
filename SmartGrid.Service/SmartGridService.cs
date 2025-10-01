using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceModel;
using SmartGrid.Common;

namespace SmartGrid.Service
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true, InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class SmartGridService : ISmartGridService
    {
        //8. 
        public delegate void TransferStartedHandler();
        public delegate void SampleReceivedHandler(SmartGridSample sample);
        public delegate void TransferCompletedHandler();
        public delegate void WarningRaisedHandler(string message, SmartGridSample sample);

        public event TransferStartedHandler OnTransferStarted;
        public event SampleReceivedHandler OnSampleReceived;
        public event TransferCompletedHandler OnTransferCompleted;
        public event WarningRaisedHandler OnWarningRaised;

        // 9. Eventovi
        public event EventHandler<FrequencySpikeEventArgs> FrequencySpike;
        public event EventHandler<OutOfBandWarningEventArgs> OutOfBandWarning;

        // 10. Event za FFT spike
        public event EventHandler<FftSpikeEventArgs> FFTSpike;

        //3. WCF servis, operacije i validacija podataka
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();
        private readonly string _filePath = "measurements_session.csv";

        // 7. Sekvencijalni streaming - flag za prenos u toku
        private bool _isStreaming = false;

        //8. Pragovi
        private readonly double fftThreshold;
        private readonly double fThreshold;

        // Prosjecna frekvencija po sesiji
        private double _avgFrequency = 0;
        private int _count = 0;

        // 9. Polja za frekvenciju
        private double _previousFrequency = 0;
        private bool _isFirstSample = true;

        // 10. Polja za FFT
        private double _previousFftMean = 0;
        private bool _isFirstFftSample = true;

        public SmartGridService()
        {
            //8. Ucitavanje pragova iz konfiguracije
            fftThreshold = double.Parse(ConfigurationManager.AppSettings["FFT_threshold"]);
            fThreshold = double.Parse(ConfigurationManager.AppSettings["F_threshold"]);

            // Pretplate na dogadjaje - izmenjeno za delegate
            OnTransferStarted += () => Console.WriteLine("[DOGADJAJ] Prenos je zapocet.");
            OnSampleReceived += (sample) => {
                Console.WriteLine("----------------------------------------------------------------");
                Console.WriteLine($"[DOGADJAJ] Primljen sample @ {sample.Timestamp}, F={sample.Frequency}");
            };
            OnTransferCompleted += () => Console.WriteLine("[DOGADJAJ] Prenos zavrsen.");
            OnWarningRaised += (message, sample) => {
                Console.WriteLine($"[UPOZORENJE] {message}");
            };

            // 9. Zadatak: eventovi za frekvenciju
            FrequencySpike += (s, e) => Console.WriteLine($"[FREQUENCY SPIKE] ΔF={e.Delta:F3}, smer: {e.Direction}");
            OutOfBandWarning += (s, e) => Console.WriteLine($"[OUT OF BAND] F={e.Frequency:F3}, Fmean={e.RunningMean:F3}, smer: {e.Direction}");

            // 10. Zadatak: event za FFT
            FFTSpike += (s, e) => Console.WriteLine($"[FFT SPIKE] ΔFFT={e.Delta:F3}, smjer: {e.Direction}");

            // TEST: upis jednog sample u test fajl
            try
            {
                using (var writer = new SampleWriter("test_sample.csv"))
                {
                    writer.WriteSample(new SmartGridSample
                    {
                        Timestamp = DateTime.Parse("2024-01-01 00:00:00"),
                        Frequency = 50.0,
                        PowerUsage = 10.0,
                        FFT1 = 0.5,
                        FFT2 = 0.5,
                        FFT3 = 0.5,
                        FFT4 = 0.5
                    });
                }
                Console.WriteLine("Uspjesno napisan testni sample.");
                Console.WriteLine("Test fajl je zatvoren - test uspjesan!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test SampleWriter greska: {ex.Message}");
            }
        }

        public void StartSession(string meta)
        {
            // 3. Validacija
            if (string.IsNullOrWhiteSpace(meta))
                throw new FaultException<ValidationFault>(new ValidationFault("Meta informacije ne mogu biti prazne."));

            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();

            // Reset proseka, polja frekvencije i FFT
            _avgFrequency = 0;
            _count = 0;
            _previousFrequency = 0;
            _isFirstSample = true;
            _previousFftMean = 0;
            _isFirstFftSample = true;

            // 6. Kreiranje fajla ako ne postoji
            if (!File.Exists(_filePath))
            {
                using (var writer = File.CreateText(_filePath)) { }
            }

            // UPDATED: Invoke delegate directly
            OnTransferStarted?.Invoke();
        }

        public void PushSample(SmartGridSample sample)
        {
            //3. Validacija
            if (sample == null)
            {
                LogReject(null, "Sample je null");
                throw new FaultException<ValidationFault>(new ValidationFault("Uzorak ne moze biti null."));
            }

            if (sample.Frequency <= 0)
            {
                LogReject(sample, $"Nevalidna frekvencija: {sample.Frequency}");
                throw new FaultException<ValidationFault>(new ValidationFault("Frekvencija mora biti veca od 0."));
            }

            if (double.IsNaN(sample.FFT1) || double.IsNaN(sample.FFT2) || double.IsNaN(sample.FFT3) || double.IsNaN(sample.FFT4))
            {
                LogReject(sample, "FFT vrednosti nisu validne.");
                throw new FaultException<ValidationFault>(new ValidationFault("FFT vrednosti moraju biti validni brojevi."));
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
            }

            //8.
            OnSampleReceived?.Invoke(sample);

            // Provjera FFT pragova
            if (sample.FFT1 > fftThreshold || sample.FFT2 > fftThreshold || sample.FFT3 > fftThreshold || sample.FFT4 > fftThreshold)
            {
                var exceededValues = new List<string>();
                if (sample.FFT1 > fftThreshold) exceededValues.Add($"FFT1={sample.FFT1:F3}");
                if (sample.FFT2 > fftThreshold) exceededValues.Add($"FFT2={sample.FFT2:F3}");
                if (sample.FFT3 > fftThreshold) exceededValues.Add($"FFT3={sample.FFT3:F3}");
                if (sample.FFT4 > fftThreshold) exceededValues.Add($"FFT4={sample.FFT4:F3}");

                string exceededValuesText = string.Join(", ", exceededValues);
                // 8.
                OnWarningRaised?.Invoke($"Prekoracen FFT prag ({fftThreshold}): {exceededValuesText}", sample);
            }

            // 10. Izracunaj prosjecan FFT i detektuj ΔFFTdiff
            double currentFftMean = (sample.FFT1 + sample.FFT2 + sample.FFT3 + sample.FFT4) / 4.0;
            if (!_isFirstFftSample)
            {
                double deltaFft = currentFftMean - _previousFftMean;
                if (Math.Abs(deltaFft) > fftThreshold)
                {
                    string direction = deltaFft > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    FFTSpike?.Invoke(this, new FftSpikeEventArgs(deltaFft, direction));
                }
            }
            else
            {
                _isFirstFftSample = false;
            }

            // Azuriranje prosjecne frekvencije
            _count++;
            _avgFrequency = ((_avgFrequency * (_count - 1)) + sample.Frequency) / _count;

            // 9. Zadatak: Frequency spike detekcija (ΔF)
            if (!_isFirstSample)
            {
                double deltaF = sample.Frequency - _previousFrequency;

                // Proveri koji je spike: |ΔF| > F_threshold
                if (Math.Abs(deltaF) > fThreshold)
                {
                    string direction = deltaF > 0 ? "iznad ocekivanog" : "ispod ocekivanog";
                    FrequencySpike?.Invoke(this, new FrequencySpikeEventArgs(deltaF, direction));
                }
            }
            else
            {
                _isFirstSample = false;
            }

            // 9. Zadatak: Out of band warning (±25% od mean)
            if (_count > 0) 
            {
                double lowerBound = _avgFrequency * 0.75;
                double upperBound = _avgFrequency * 1.25;

                if (sample.Frequency < lowerBound)
                {
                    OutOfBandWarning?.Invoke(this, new OutOfBandWarningEventArgs(sample.Frequency, _avgFrequency, "ispod ocekivane vrednosti"));
                }
                else if (sample.Frequency > upperBound)
                {
                    OutOfBandWarning?.Invoke(this, new OutOfBandWarningEventArgs(sample.Frequency, _avgFrequency, "iznad ocekivane vrednosti"));
                }
            }

            if (Math.Abs(sample.Frequency - _avgFrequency) > _avgFrequency * 0.25)
            {
                //8.
                OnWarningRaised?.Invoke($"Frekvencija {sample.Frequency} odstupa vise od ±25% od tekuceg prosjeka {_avgFrequency:F2}", sample);
            }

            if (Math.Abs(sample.Frequency - fThreshold) > fThreshold * 0.25)
            {
                OnWarningRaised?.Invoke($"Frekvencija van opsega ±25% od {fThreshold}Hz. Izmjereno: {sample.Frequency}", sample);
            }

            // Update novu F
            _previousFrequency = sample.Frequency;

            // Update prethodni FFT mean
            _previousFftMean = currentFftMean;

            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }

        public void EndSession()
        {
            // 7.Zavrsen prenos
            if (_isStreaming)
            {
                Console.WriteLine("Zavrsen prenos");
                _isStreaming = false;
            }

            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");

            // 8.
            OnTransferCompleted?.Invoke();
        }

        //6. reject.csv
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

    // 9.
    public class FrequencySpikeEventArgs : EventArgs
    {
        public double Delta { get; }
        public string Direction { get; }

        public FrequencySpikeEventArgs(double delta, string direction)
        {
            Delta = delta;
            Direction = direction;
        }
    }

    public class OutOfBandWarningEventArgs : EventArgs
    {
        public double Frequency { get; }
        public double RunningMean { get; }
        public string Direction { get; }

        public OutOfBandWarningEventArgs(double frequency, double runningMean, string direction)
        {
            Frequency = frequency;
            RunningMean = runningMean;
            Direction = direction;
        }
    }

    // 10. Zadatak: analiza FFT
    public class FftSpikeEventArgs : EventArgs
    {
        public double Delta { get; }
        public string Direction { get; }

        public FftSpikeEventArgs(double delta, string direction)
        {
            Delta = delta;
            Direction = direction;
        }
    }
}
