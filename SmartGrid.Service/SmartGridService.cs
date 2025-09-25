using System;
using System.Collections.Generic;
using System.ServiceModel;
using SmartGrid.Common;
using System.IO;

namespace SmartGrid.Service
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class SmartGridService : ISmartGridService
    {
        //3. Zadatak: WCF servis, operacije i validacija podataka
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();
        private readonly string _filePath = "measurements_session.csv";

        public void StartSession(string meta)
        {
            if (string.IsNullOrWhiteSpace(meta))
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Meta informacije ne mogu biti prazne."));

            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();

            if (!File.Exists(_filePath))
            {
                using (var writer = File.CreateText(_filePath)) { }
            }
        }

        public void PushSample(SmartGridSample sample)
        {
            if (sample == null)
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Uzorak ne moze biti null."));

            if (sample.Frequency <= 0)
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Frekvencija mora biti veca od 0."));

            if (double.IsNaN(sample.FFT1) || double.IsNaN(sample.FFT2) ||
                double.IsNaN(sample.FFT3) || double.IsNaN(sample.FFT4))
                throw new FaultException<ValidationFault>(
                    new ValidationFault("FFT vrednosti moraju biti validni brojevi."));

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
                throw new FaultException<ValidationFault>(
                    new ValidationFault($"Greska pri snimanju uzorka: {ex.Message}"));
            }

            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }

        public void EndSession()
        {
            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");
        }
    }
}
