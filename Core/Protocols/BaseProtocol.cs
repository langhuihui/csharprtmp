using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks.Dataflow;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    public interface IInFileRTMPStreamHolder
    {
        event Action OnReadyForSend;
    }
    public abstract class BaseProtocol : IDisposable
    {
        public static uint _idGenerator;
        protected bool _enqueueForDelete;
        protected bool _gracefullyEnqueueForDelete;
        public Variant CustomParameters;
        public readonly DateTime CreationTimestamp = DateTime.Now;
        protected BaseProtocol _farProtocol;
        protected BaseProtocol _nearProtocol;
        protected BaseClientApplication _application;
        public readonly uint Id;
        public readonly ulong Type;

        protected BaseProtocol()
        {
            Id = ++_idGenerator;
            Type = this.GetAttribute<ProtocolTypeAttribute>().First().Type;
            DeleteFarProtocol = true;
            DeleteNearProtocol = true;
            this.RegisterProtocol();
        }

        public virtual BaseClientApplication Application
        {
            get { return _application; }
            set
            {
                //1. Get the old and the new application name and id
               // string oldAppName = "(none)";
                uint oldAppId = 0;
               // string newAppName = "(none)";
                uint newAppId = 0;
                if (_application != null)
                {
                    //oldAppName = _application.Name;
                    oldAppId = _application.Id;
                }
                if (value != null)
                {
                    //newAppName = value.Name;
                    newAppId = value.Id;
                }

                //2. Are we landing on the same application?
                if (oldAppId == newAppId)
                {
                    return;
                }

                //3. If the application is the same, return. Otherwise, unregister
                _application?.UnRegisterProtocol(this);

                //4. Setup the new application
                _application = value;

                //5. Register to it
                _application?.RegisterProtocol(this);
            }
        }

        
        public virtual BaseProtocol FarProtocol
        {
            get { return _farProtocol; }
            protected set
            {
                if (value == null)
                {
                    if (_farProtocol != null)
                        _farProtocol._nearProtocol = null;
                    _farProtocol = null;
                    return;
                }
                if (!AllowFarProtocol(value.Type))
                {
                    Logger.ASSERT("Protocol {0} can't accept a far protocol of type: {1}", Type.TagToString(), value.Type.TagToString());
                }
                if (!value.AllowNearProtocol(Type))
                {
                    Logger.ASSERT("Protocol {0} can't accept a near protocol of type: {1}", Type.TagToString(), value.Type.TagToString());
                }
                if (_farProtocol == null)
                {
                    _farProtocol = value;
                    value.NearProtocol = this;
                }
                else
                {
                    if (_farProtocol != value)
                    {
                        Logger.ASSERT("Far protocol already present");
                    }
                }

            }
        }

        public BaseProtocol NearProtocol
        {
            get { return _nearProtocol; }
            set
            {
                if (value == null)
                {
                    if (_nearProtocol != null)
                        _nearProtocol._farProtocol = null;
                    _nearProtocol = null;
                    return;
                }
                if (!AllowNearProtocol(value.Type))
                {
                    Logger.ASSERT("Protocol {0} can't accept a near protocol of type: {1}", Type.TagToString(), value.Type.TagToString());
                }
                if (!value.AllowFarProtocol(Type))
                {
                    Logger.ASSERT("Protocol {0} can't accept a far protocol of type: {1}", Type.TagToString(), value.Type.TagToString());
                }
                if (_nearProtocol == null)
                {
                    _nearProtocol = value;
                    value.FarProtocol = this;
                }
                else
                {
                    if (_nearProtocol != value)
                    {
                        Logger.ASSERT("Near protocol already present");
                    }
                }
            }
        }

        public bool DeleteFarProtocol { set; protected get; }
        public bool DeleteNearProtocol { set; protected get; }
        virtual public IOHandler IOHandler {
            get { return FarProtocol?.IOHandler; }
            set { if(FarProtocol!=null) FarProtocol.IOHandler = value; } }

        public virtual bool SignalInputData(int recAmount) => false;

        public virtual bool AllowNearProtocol(ulong type)
        {
            var attr = GetType().GetCustomAttribute<AllowNearTypesAttribute>();
            return attr!=null && (attr.Types.Length == 0 || attr.Types.Contains(type));
        }

        public virtual bool AllowFarProtocol(ulong type)
        {
            var attr = GetType().GetCustomAttribute<AllowFarTypesAttribute>();
            return attr != null && (attr.Types.Length == 0 || attr.Types.Contains(type));
        }

        public virtual InputStream InputBuffer => _farProtocol?.InputBuffer;

        public virtual MemoryStream OutputBuffer => _nearProtocol?.OutputBuffer;
        public virtual BaseProtocol FarEndpoint => _farProtocol?.FarEndpoint ?? this;
        public virtual BaseProtocol NearEndpoint => _nearProtocol?.NearEndpoint ?? this;

        public virtual void EnqueueForDelete()
        {
            if (!_enqueueForDelete)
            {
                _enqueueForDelete = true;
                ProtocolManager.EnqueueForDelete(this);
            }
        }
        public virtual void GracefullyEnqueueForDelete(bool fromFarSide = true)
        {
            if (fromFarSide)
            {
                FarEndpoint.GracefullyEnqueueForDelete(false);
                return;
            }


            _gracefullyEnqueueForDelete = true;
            if (OutputBuffer != null)
            {
                return;
            }

            if (_nearProtocol != null)
            {
                _nearProtocol.GracefullyEnqueueForDelete(false);
            }
            else
            {
                EnqueueForDelete();
            }
        }

        public virtual ulong GetDecodedBytesCount() => _farProtocol?.GetDecodedBytesCount() ?? 0;

        public virtual bool EnqueueForOutbound(MemoryStream outputStream,int offset = 0) => _farProtocol == null || _farProtocol.EnqueueForOutbound(outputStream,offset);

        public virtual void GetStats(Variant info, uint namespaceId)
        {
            info.Add("id",(((ulong)namespaceId)<<32)|Id);
            info.Add("type",Type.TagToString());
            info.Add("creationTimestamp",CreationTimestamp);
            info.Add("queryTimestamp",DateTime.Now);
            info.Add("isEnqueueForDelete",IsEnqueueForDelete);
            if (_application != null)
            {
                info.Add("applicationId", (((ulong)namespaceId) << 32) | _application.Id);
            }else 
                info.Add("applicationId",((ulong)namespaceId)<<32);
        }

        //public virtual void SignalInterProtocolEvent(Variant e) => NearProtocol?.SignalInterProtocolEvent(e);

        public virtual bool SignalInputData(InputStream inputStream, IPEndPoint address)
        {
            Logger.WARN("This should be overridden. Protocol type is {0}", Type.TagToString());
            return false;
        }

        public bool IsEnqueueForDelete => _enqueueForDelete || _gracefullyEnqueueForDelete;

        public virtual void ReadyForSend()
        {
            if (_gracefullyEnqueueForDelete)
            {
                EnqueueForDelete();
                return;
            }
            NearProtocol?.ReadyForSend();
        }

        public virtual bool TimePeriodElapsed() => NearProtocol == null || NearProtocol.TimePeriodElapsed();

        public Variant GetStackStats(Variant info, uint namespaceId)
        {
            IOHandler pIOHandler = IOHandler;
            info["carrier"] = Variant.Get();
            pIOHandler?.GetStats(info["carrier"], namespaceId);
            var pTemp = FarEndpoint;
            info["stack"] = Variant.Get();
            while (pTemp != null)
            {
                var item = Variant.Get();
                pTemp.GetStats(item, namespaceId);
                info["stack"].Add(item);
                pTemp = pTemp.NearProtocol;
            }
            return info;
        }

        public virtual bool Initialize(Variant parameters)
        {
            //Logger.WARN("You should override bool BaseProtocol::Initialize(Variant &parameters) on protocol {0}",
           // Type.TagToString());
            CustomParameters = parameters;
            return true;
        }

        public void ResetFarProtocol()
        {
            if (_farProtocol != null)
                _farProtocol._nearProtocol = null;
            _farProtocol = null;
        }
        public virtual void Dispose()
        {
            if (_farProtocol != null)
            {
                _farProtocol._nearProtocol = null;
                if (DeleteFarProtocol)
                    _farProtocol.EnqueueForDelete();
                _farProtocol = null;
            }
            if (_nearProtocol != null)
            {
                _nearProtocol._farProtocol = null;
                if (DeleteNearProtocol)
                    _nearProtocol.EnqueueForDelete();
                _nearProtocol = null;
            }

            this.UnRegisterProtocol();
        }
    }
}
