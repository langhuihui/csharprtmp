using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Cluster;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public sealed class SOManager:IDisposable
    {
        private readonly Dictionary<string, SO> _sos = new Dictionary<string, SO>();
        public BaseClientApplication Application;
        public SOManager(BaseClientApplication application)
        {
            Application = application;
            var soPath = application.SOPath;
            if (!Directory.Exists(soPath)) Directory.CreateDirectory(soPath);
            foreach (var name in Directory.GetFiles(soPath, "*.so").Select(Path.GetFileNameWithoutExtension))
                _sos[name] = new SO(Application, name, true);
        }
        public void UnRegisterProtocol(BaseProtocol protocol)
        {
            foreach (var so in _sos.Values.Where(so => so.UnRegisterProtocol(protocol) && so.SubscribersCount == 0 && !so.IsPersistent).ToList())
            {
                _sos.Remove(so.Name);
            }
        }

        public void RegisterProtocol(BaseClusterProtocol protocol)
        {
            foreach (var so in _sos.Values)
            {
                so.RegisterProtocol(protocol);
                protocol.SOs.Add(so);
                so.Track();
            }
        }
        public SO this[string name, bool persistent = false] => _sos.ContainsKey(name) ? _sos[name] : _sos[name] = new SO(Application, name, persistent);

        public bool Process(BaseRTMPProtocol pFrom, Variant request)
        {
            string name = request [ Defines.RM_SHAREDOBJECT][Defines.RM_SHAREDOBJECT_NAME];
            var so = this[name, request[Defines.RM_SHAREDOBJECT][Defines.RM_SHAREDOBJECT_PERSISTENCE]];
            var ps = request[Defines.RM_SHAREDOBJECT,Defines.RM_SHAREDOBJECT_PRIMITIVES];
            if(ps != null)
            for (var i = 0; i < ps.Count; i++)
            {
                if (ProcessSharedObjectPrimitive(pFrom, so, name, request, i)) continue;
                Logger.FATAL("Unable to process primitive {0} from {1}",i,request);
                return false;
            }
            if (_sos.TryGetValue(name, out so)) so.Track();
            return true;
        }

        public void Process(BaseProtocol pFrom,string name,bool isPersistent,Variant primitives)
        {
            var so = this[name, isPersistent];
            for (var i = 0; i < primitives.Count; i++)
            {
                var primitive = primitives[i];
                switch ((byte)primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE])
                {
                    case Defines.SOT_SC_UPDATE_DATA:
                        var key = primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD].Children.Keys.First();
                        so.Set(key, primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD][key],pFrom);
                        break;
                    case Defines.SOT_SC_DELETE_DATA:
                        so.UnSet(primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD][0], pFrom);
                        break;
                    case Defines.SOT_SC_INITIAL_DATA:
                    case Defines.SOT_SC_CLEAR_DATA:
                        so.Clear(pFrom);
                        break;
                }
            }
            if (_sos.TryGetValue(name, out so)) so.Track();
        }
        private bool ProcessSharedObjectPrimitive(BaseRTMPProtocol pFrom, SO pSO, string name, Variant request,
            int primitiveId)
        {
            var primitive = request[Defines.RM_SHAREDOBJECT][Defines.RM_SHAREDOBJECT_PRIMITIVES][ primitiveId];
            switch ((byte)primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE])
            {
                case Defines.SOT_CS_CONNECT:
                    pSO.RegisterProtocol(pFrom);
                    return true;
                case Defines.SOT_CS_DISCONNECT:
                    UnRegisterProtocol(pFrom);
                    return true;
                case Defines.SOT_CSC_DELETE_DATA:
                    pSO.UnSet(primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD],pFrom);
                    return true;
                case Defines.SOT_CS_SET_ATTRIBUTE:
                    if (pSO == null)
                    {
                        Logger.FATAL("SO is null");
                        return false;
                    }
                    foreach (KeyValuePair<string,Variant> item in primitive [Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD])
                    {
                        pSO.Set(item.Key, item.Value.Clone(), pFrom);
                    }
                    return true;
                default:
                    Logger.FATAL("SO primitive not allowed here");
                    return false;
            }
        }

        public void Dispose()
        {
            //foreach (var so in _sos)
            //{
            //    so.Dispose();
            //}
            _sos.Clear();
        }
    }
}
