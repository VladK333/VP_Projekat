using System;
using System.Collections.Generic;
using SmartGrid.Common;

namespace SmartGrid.Service
{
    public class SmartGridService : ISmartGridService
    {
        private readonly List<SmartGridSample> _samples = new List<SmartGridSample>();

        public void StartSession(string meta)
        {
            Console.WriteLine($"Sesija zapoceta: {meta}");
            _samples.Clear();
        }

        public void PushSample(SmartGridSample sample)
        {
            _samples.Add(sample);
            Console.WriteLine($"Primljen uzorak: {sample.Timestamp}, Frekvencija: {sample.Frequency}");
        }

        public void EndSession()
        {
            Console.WriteLine($"Sesija zavrsena. Ukupan broj uzoraka: {_samples.Count}");
        }
    }
}
