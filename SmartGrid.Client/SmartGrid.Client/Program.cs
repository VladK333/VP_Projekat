using System;
using System.Collections.Generic;
using System.ServiceModel;
using SmartGrid.Common;

namespace SmartGrid.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1. Putanja do CSV fajla
            string datasetPath = @"smart_grid_dataset.csv";

            // 2. Učitaj prvih 100 uzoraka
            var loader = new CsvLoader(datasetPath);
            List<SmartGridSample> samples = loader.LoadFirst100();

            Console.WriteLine($"Ucitanih {samples.Count} validnih uzoraka.");

            // 3. Kreiranje WCF klijenta
            var binding = new NetTcpBinding
            {
                TransferMode = TransferMode.Streamed,
                MaxReceivedMessageSize = 10485760,
                OpenTimeout = TimeSpan.FromMinutes(1),
                CloseTimeout = TimeSpan.FromMinutes(1),
                ReceiveTimeout = TimeSpan.FromMinutes(10),
                SendTimeout = TimeSpan.FromMinutes(1)
            };

            var endpoint = new EndpointAddress("net.tcp://localhost:4000/SmartGridService");
            var channelFactory = new ChannelFactory<ISmartGridService>(binding, endpoint);
            ISmartGridService client = channelFactory.CreateChannel();

            try
            {
                // 4. Start session
                client.StartSession("Test sesija sa klijenta");

                // 5. Push prvih 100 uzoraka
                foreach (var sample in samples)
                {
                    try
                    {
                        client.PushSample(sample);
                        Console.WriteLine($"Poslat uzorak: {sample.Timestamp}");
                    }
                    catch (FaultException<ValidationFault> ex)
                    {
                        Console.WriteLine($"Odbačen uzorak: {ex.Detail.Message}");
                    }
                }

                // 6. End session
                client.EndSession();
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
