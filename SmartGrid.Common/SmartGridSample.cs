using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace SmartGrid.Common
{
    [DataContract]
    public class SmartGridSample
    {
        //2. Zadatak: WCF servis, konfiguracija i ugovori -> polja za DataContract
        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public double FFT1 { get; set; }

        [DataMember]
        public double FFT2 { get; set; }

        [DataMember]
        public double FFT3 { get; set; }

        [DataMember]
        public double FFT4 { get; set; }

        [DataMember]
        public double PowerUsage { get; set; }

        [DataMember]
        public double Frequency { get; set; }
    }
}
