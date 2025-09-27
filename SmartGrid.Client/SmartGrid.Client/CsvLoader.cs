using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid.Common;

namespace SmartGrid.Client
{
    public class CsvLoader
    {
        //5. Zadatak: Rad sa fajlovima, učitavanje CSV na klijentu
        private readonly string _datasetPath;
        private readonly string _logPath = "invalid_rows.log";

        public CsvLoader(string datasetPath)
        {
            _datasetPath = datasetPath ?? throw new ArgumentNullException(nameof(datasetPath));
        }

        public List<SmartGridSample> LoadFirst100()
        {
            var samples = new List<SmartGridSample>();
            int count = 0;

            using (var reader = new StreamReader(_datasetPath))
            using (var logWriter = new StreamWriter(_logPath, append: true))
            {
                string header = reader.ReadLine(); // preskoči zaglavlje
                while (!reader.EndOfStream && count < 100) //max 100 redova
                {
                    string line = reader.ReadLine();
                    var parts = line.Split(',');

                    try
                    {
                        if (parts.Length < 7)
                            throw new FormatException("Premalo kolona u redu.");

                        var sample = new SmartGridSample //parsiranje u SmartGridSample
                        {
                            Timestamp = DateTime.Parse(parts[0], CultureInfo.InvariantCulture),
                            // Correct column mappings:
                            Frequency = double.Parse(parts[4], CultureInfo.InvariantCulture), // Frequency (Hz)
                            PowerUsage = double.Parse(parts[3], CultureInfo.InvariantCulture), // Power Usage (kW)
                            FFT1 = double.Parse(parts[6], CultureInfo.InvariantCulture),  // FFT_1
                            FFT2 = double.Parse(parts[7], CultureInfo.InvariantCulture),  // FFT_2
                            FFT3 = double.Parse(parts[8], CultureInfo.InvariantCulture),  // FFT_3
                            FFT4 = double.Parse(parts[9], CultureInfo.InvariantCulture)   // FFT_4
                        };

                        samples.Add(sample);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        logWriter.WriteLine($"[GRESKA] {ex.Message} | Red: {line}");
                    }
                }
            }

            return samples;
        }
    }
}
