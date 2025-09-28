using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
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
        public delegate void TransferStartedHandler();
        public delegate void SampleReceivedHandler(SmartGridSample sample);
        public delegate void TransferCompletedHandler();
        public delegate void WarningRaisedHandler(string message, SmartGridSample sample);

        public event TransferStartedHandler OnTransferStarted;
        public event SampleReceivedHandler OnSampleReceived;
        public event TransferCompletedHandler OnTransferCompleted;
        public event WarningRaisedHandler OnWarningRaised;

        // 9. Zadatak: eventovi
        public event EventHandler<FrequencySpikeEventArgs> FrequencySpike;
        public event EventHandler<OutOfBandWarningEventArgs> OutOfBandWarning;

        // 10. Zadatak: event za FFT spike
        public event EventHandler<FftSpikeEventArgs> FFTSpike;

        //3. Zadatak: WCF servis, operacije i validacija podataka
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();
        private readonly string _filePath = "measurements_session.csv";
        // 7. Zadatak: sekvencijalni streaming - flag za prenos u toku
        private bool _isStreaming = false;

        //8. Pragovi
        private readonly double fftThreshold;
        private readonly double fThreshold;

        // Prosjecna frekvencija po sesiji
        private double _avgFrequency = 0;
        private int _count = 0;

        // 9. Zadatak: polja za frekvenciju
        private double _previousFrequency = 0;
        private bool _isFirstSample = true;

        // 10. Zadatak: polja za FFT
        private double _previousFftMean = 0;
        private bool _isFirstFftSample = true;

        public SmartGridService()
        {
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
            FrequencySpike += (s, e) =>
                Console.WriteLine($"[FREQUENCY SPIKE] ΔF={e.Delta:F3}, smer: {e.Direction}");
            OutOfBandWarning += (s, e) =>
                Console.WriteLine($"[OUT OF BAND] F={e.Frequency:F3}, Fmean={e.RunningMean:F3}, smer: {e.Direction}");

            // 10. Zadatak: event za FFT
            FFTSpike += (s, e) =>
                Console.WriteLine($"[FFT SPIKE] ΔFFT={e.Delta:F3}, smjer: {e.Direction}");
        }

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

            // Reset polja frekvencije
            _previousFrequency = 0;
            _isFirstSample = true;

            // Reset polja FFT
            _previousFftMean = 0;
            _isFirstFftSample = true;

            // Kreiranje fajla ako ne postoji
            if (!File.Exists(_filePath))
            {
                using (var writer = File.CreateText(_filePath)) { }
            }

            // UPDATED: Invoke delegate directly
            OnTransferStarted?.Invoke();
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

            //8. UPDATED: Invoke delegate with sample parameter
            OnSampleReceived?.Invoke(sample);

            // Provjera FFT pragova
            if (sample.FFT1 > fftThreshold || sample.FFT2 > fftThreshold ||
                sample.FFT3 > fftThreshold || sample.FFT4 > fftThreshold)
            {
                var exceededValues = new List<string>();
                if (sample.FFT1 > fftThreshold) exceededValues.Add($"FFT1={sample.FFT1:F3}");
                if (sample.FFT2 > fftThreshold) exceededValues.Add($"FFT2={sample.FFT2:F3}");
                if (sample.FFT3 > fftThreshold) exceededValues.Add($"FFT3={sample.FFT3:F3}");
                if (sample.FFT4 > fftThreshold) exceededValues.Add($"FFT4={sample.FFT4:F3}");

                string exceededValuesText = string.Join(", ", exceededValues);
                // UPDATED: Invoke delegate with message and sample parameters
                OnWarningRaised?.Invoke($"Prekoracen FFT prag ({fftThreshold}): {exceededValuesText}", sample);
            }

            // 10. Zadatak: izracunaj prosjecan FFT i detektuj ΔFFTdiff
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
            if (_count > 0) // proveri samo ako imas mean
            {
                double lowerBound = _avgFrequency * 0.75;
                double upperBound = _avgFrequency * 1.25;

                if (sample.Frequency < lowerBound)
                {
                    OutOfBandWarning?.Invoke(this,
                        new OutOfBandWarningEventArgs(sample.Frequency, _avgFrequency, "ispod ocekivane vrednosti"));
                }
                else if (sample.Frequency > upperBound)
                {
                    OutOfBandWarning?.Invoke(this,
                        new OutOfBandWarningEventArgs(sample.Frequency, _avgFrequency, "iznad ocekivane vrednosti"));
                }
            }

            if (Math.Abs(sample.Frequency - _avgFrequency) > _avgFrequency * 0.25)
            {
                OnWarningRaised?.Invoke(
                    $"Frekvencija {sample.Frequency} odstupa vise od ±25% od tekuceg prosjeka {_avgFrequency:F2}",
                    sample);
            }

            // Provjera prema fiksnom pragu
            if (Math.Abs(sample.Frequency - fThreshold) > fThreshold * 0.25)
            {
                OnWarningRaised?.Invoke(
                    $"Frekvencija van opsega ±25% od {fThreshold}Hz. Izmjereno: {sample.Frequency}",
                    sample);
            }

            // Update novu F, radi sledeceg sample
            _previousFrequency = sample.Frequency;

            // Update prethodni FFT mean, radi sledeceg sample
            _previousFftMean = currentFftMean;

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

            // UPDATED: Invoke delegate directly
            OnTransferCompleted?.Invoke();
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