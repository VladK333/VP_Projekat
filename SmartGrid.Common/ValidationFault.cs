using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SmartGrid.Common
{
    [DataContract]
    public class ValidationFault
    {
        //2. Zadatak: WCF servis, konfiguracija i ugovori -> Validation
        string message;

        public ValidationFault(string message)
        {
            this.Message = message;
        }

        [DataMember]
        public string Message { get => message; set => message = value; }
    }
}
