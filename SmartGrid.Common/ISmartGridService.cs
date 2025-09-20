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
        void StartSession(string meta);

        [OperationContract]
        void PushSample(SmartGridSample sample);

        [OperationContract]
        void EndSession();
    }
}
