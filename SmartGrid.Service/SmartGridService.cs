using System;
using System.Collections.Generic;
using System.ServiceModel;
using SmartGrid.Common;

namespace SmartGrid.Service
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class SmartGridService : ISmartGridService
    {
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();

        public void StartSession(string meta)
        {
            if (string.IsNullOrWhiteSpace(meta))
                throw new FaultException<ValidationFault>(
                    new ValidationFault("Meta informacije ne mogu biti prazne."));

            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();
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
            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }

        public void EndSession()
        {
            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");
        }
    }
}
