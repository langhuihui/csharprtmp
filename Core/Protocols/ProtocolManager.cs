using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSharpRTMP.Common;

namespace CSharpRTMP.Core.Protocols
{
    public interface IManage
    {
        void Manage();
    }
    public static class ProtocolManager
    {
        public static readonly Dictionary<uint, BaseProtocol> ActiveProtocols = new Dictionary<uint, BaseProtocol>();
        public static readonly Dictionary<uint, BaseProtocol> DeadProtocols = new Dictionary<uint, BaseProtocol>();
        public static readonly HashSet<IManage> ManageProtocols = new HashSet<IManage>();
        public static void RegisterProtocol(this BaseProtocol pProtocol)
        {
            if (!ActiveProtocols.ContainsKey(pProtocol.Id) && !DeadProtocols.ContainsKey(pProtocol.Id))
            {
                ActiveProtocols[pProtocol.Id] = pProtocol;
                if(pProtocol is IManage)
                ManageProtocols.Add(pProtocol as IManage);
            }
        }
        public static void UnRegisterProtocol(this BaseProtocol pProtocol)
        {
            //if (ActiveProtocols.ContainsKey(pProtocol.Id))
            //{
            //    ActiveProtocols.Remove(pProtocol.Id);
            //}
            //else if (DeadProtocols.ContainsKey(pProtocol.Id))
            //{
            //    DeadProtocols.Remove(pProtocol.Id);
            //}
          
        }
        public static void EnqueueForDelete(this BaseProtocol pProtocol)
        {
            if (pProtocol.NearProtocol == null)
            {
                pProtocol.Log().Info("Enqueue for delete for protocol {0}", pProtocol.ToString());
            }
            pProtocol.Application = null;
            if (ActiveProtocols.ContainsKey(pProtocol.Id))
            {
                ActiveProtocols.Remove(pProtocol.Id);
            }
            if (!DeadProtocols.ContainsKey(pProtocol.Id))
            {
                DeadProtocols[pProtocol.Id] = pProtocol;
            }
           
        }

        public static void Manage()
        {
#if PARALLEL
             Parallel.ForEach(ManageProtocols, x => x.Manage());
#else
            foreach (var manageProtocol in ManageProtocols)
            {
                manageProtocol.Manage();
            }
#endif
            Thread.Sleep(2000);
        }
        public static int CleanupDeadProtocols()
        {
            var result = DeadProtocols.Count;
            foreach (var deadProtocol in DeadProtocols.Keys.ToArray())
            {
                DeadProtocols[deadProtocol].Dispose();
                if (DeadProtocols[deadProtocol] is IManage)
                    ManageProtocols.Remove(DeadProtocols[deadProtocol] as IManage);
                DeadProtocols.Remove(deadProtocol);
            }
            return result;
        }

        public static void Shutdown()
        {
            while (ActiveProtocols.Count > 0)
            {
                EnqueueForDelete(ActiveProtocols[0]);
            }
        }

        public static BaseProtocol GetProtocol(uint id,
                bool includeDeadProtocols = false)
        {
            return includeDeadProtocols || !DeadProtocols.ContainsKey(id)
                ? (ActiveProtocols.ContainsKey(id)
                    ? ActiveProtocols[id]
                    : (DeadProtocols.ContainsKey(id) ? DeadProtocols[id] : null))
                : null;
        }

    }
}
