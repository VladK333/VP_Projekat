using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SmartGrid.Service
{
    class Program
    {
        static void Main(string[] args)
        {
            ServiceHost host = new ServiceHost(typeof(SmartGridService));
            host.Open();

            Console.WriteLine("SmartGrid servis je pokrenut...");
            Console.ReadKey();

            host.Close();
            Console.WriteLine("SmartGrid servis je zatvoren.");
        }
    }
}
