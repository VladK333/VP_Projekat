using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using SmartGrid.Common;

namespace SmartGrid.Client
{
    class Program
    {
        static void Main(string[] args)
        {

            // Putanja do CSV fajla
            string datasetPath = @"smart_grid_dataset.csv";
            //[TEST 1] 4. Simulacija prekida i test Dispose
            List<SmartGridSample> samples;
            try
            {
                using (var loader = new CsvLoader(datasetPath)) // 5. zadatak
                {
                    samples = loader.LoadFirst100();
                    throw new Exception("Simulacija prekida prenosa");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Izuzetak: " + ex.Message);
            }

            // Provjera da li je fajl zatvoren
            try
            {
                using (var fs = File.OpenRead(datasetPath))
                {
                    Console.WriteLine("Fajl je zatvoren i može se ponovo otvoriti – test uspjesan!");
                }
            }
            catch (IOException ioex)
            {
                Console.WriteLine("Fajl je jos uvijek otvoren – test neuspjesan: " + ioex.Message);
            }


            using (var loader = new CsvLoader(datasetPath))// 5. zadatak
            {
                samples = loader.LoadFirst100();
            }

            Console.WriteLine($"Ucitanih {samples.Count} validnih uzoraka.");

            // Kreiranje WCF klijenta
            ChannelFactory < ISmartGridService> channelFactory = new ChannelFactory<ISmartGridService>("SmartGridService");
            ISmartGridService client = channelFactory.CreateChannel();

            try
            {
                // Start session
                client.StartSession("Test sesija sa klijenta");

                // Push prvih 100 uzoraka
                foreach (var sample in samples)
                {
                    try
                    {
                        client.PushSample(sample); // 7. Zadatak: server ce prikazati "Prenos u toku..."
                        Console.WriteLine($"Poslat uzorak: {sample.Timestamp}");
                    }
                    catch (FaultException<ValidationFault> ex)
                    {
                        Console.WriteLine($"Odbačen uzorak: {ex.Detail.Message}");
                    }
                }

                // End session
                client.EndSession(); // 7. Zadatak: server ce prikazati "Završen prenos"
                Console.WriteLine("Sesija završena.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greška prilikom komunikacije sa servisom: " + ex.Message);
            }
            finally
            {
                ((IClientChannel)client).Close();
                channelFactory.Close();
            }

            Console.WriteLine("Pritisnite ENTER za izlaz...");
            Console.ReadLine();
        }
    }
}
