using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace SmartGrid.Common
{
    [ServiceContract]
    public interface ISmartGridService
    {
        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        void StartSession(string meta);

        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        void PushSample(SmartGridSample sample);

        [OperationContract]
        [FaultContract(typeof(ValidationFault))]
        void EndSession();
    }
}
