using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols.Cluster;

namespace CSharpRTMP.Core.Protocols.Rtmp
{
    public struct DirtyInfo
    {
        public string PropertyName;
        public byte Type;
    }
    public sealed class SO
    {
        public readonly string Name;
        public readonly bool IsPersistent;
        private readonly ConcurrentDictionary<BaseProtocol, List<DirtyInfo>> _dirtyPropsByProtocol = new ConcurrentDictionary<BaseProtocol, List<DirtyInfo>>();
        private bool _versionIncremented;
        public readonly Variant Payload;
        public uint Version { get; private set; }
        private readonly string _filePath;
        public BaseClientApplication Application;
        public event Action<DirtyInfo> Synchronization;
        public SO(BaseClientApplication app,string name, bool persistent)
        {
            Name = name;
            Application = app;
            IsPersistent = persistent;
            Version = 1;
            if (persistent)
            {
                _filePath = app.SOPath + Name + ".so";
                if (File.Exists(_filePath))
                {
                    Variant.DeserializeFromFile(_filePath, out Payload);
                }
                else
                {
                    Payload = Variant.Get();
                    //File.Create(_filePath).Close();
                }
            }
            else
            {
                Payload = Variant.Get();
            }
            if (ClientApplicationManager.ClusterApplication != null)
                ClientApplicationManager.ClusterApplication.GetProtocolHandler<BaseClusterAppProtocolHandler>().OnSOCreated(this);
        }
        public int SubscribersCount => _dirtyPropsByProtocol.Count;

        public string[] PropertyNames => ((VariantMap)Payload).Keys.ToArray();

        public int PropertiesCount => Payload.Count;

        public bool HasProperties => Payload.Count > 0;

        public bool HasProperty(string propertyName) => Payload[propertyName] != null;

        public string DumpTrack()
        {
            var result = $"SO: {Name}; Ver:{Version}\n";
            foreach (var item in _dirtyPropsByProtocol)
            {
                result += "Protocol:" + item.Key;
                result += string.Join(Environment.NewLine,
                    item.Value.Select(x => "\tKey: " + x.PropertyName + ";Type:" + x.Type));
            }
            return result;
        }

        private Variant ToPrimitives(List<DirtyInfo> info)
        {
            var vm = GlobalPool<VariantMap>.GetObject();
            var result = Variant.Get(vm);
            vm.IsArray = true;
            vm.ArrayLength = info.Count;
            for (var i = 0; i < vm.ArrayLength; i++)
            {
                var primitive = Variant.GetMap(new VariantMapHelper { { Defines.RM_SHAREDOBJECTPRIMITIVE_TYPE, info[i].Type } });
                switch (info[i].Type)
                {
                    case Defines.SOT_SC_UPDATE_DATA_ACK:
                         
                    case Defines.SOT_SC_DELETE_DATA:
                        primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD] = Variant.GetList(info[i].PropertyName);
                        break;
                    case Defines.SOT_SC_UPDATE_DATA:
                        primitive[Defines.RM_SHAREDOBJECTPRIMITIVE_PAYLOAD] = Variant.GetMap(new VariantMapHelper
                        {
                            {info[i].PropertyName, Payload[info[i].PropertyName].Clone()}
                        });
                        break;
                    case Defines.SOT_SC_INITIAL_DATA:
                    case Defines.SOT_SC_CLEAR_DATA:
                        break;
                    default:
                        Logger.ASSERT("Unable to handle primitive type:{0}", info[i].Type);
                        break;
                }
                vm[VariantMap.GetIndexString(i)] = primitive;
            }
            return result;
        }
        public void Track()
        {
            foreach (var dirty in _dirtyPropsByProtocol.Where(x=>x.Value.Count>0))
            {
                var pTo = dirty.Key;
                var primitives = ToPrimitives(dirty.Value);
                //var message = SOMessageFactory.GetSharedObject(3, 0, 0, false, Name, Version, IsPersistent);
                //message[Defines.RM_SHAREDOBJECT, Defines.RM_SHAREDOBJECT_PRIMITIVES] = new Variant(primitives.ToList());
                if (pTo != null)
                {
                    if (pTo is BaseClusterProtocol)
                        (pTo as BaseClusterProtocol).SharedObjectTrack(Application, Name, Version, IsPersistent, primitives);
                    else 
                        pTo.Application.SharedObjectTrack(pTo, Name, Version, IsPersistent,primitives);
                }
                //if (pTo != null && !pTo.SendMessage(message, true))
                //{
                //    pTo.EnqueueForDelete();
                //}
                dirty.Value.Clear();
            }
            
            //foreach (var dirtyProps in _dirtyPropsByProtocol)
            //{
            //    dirtyProps.Value.Clear();
            //}
            if (IsPersistent && _versionIncremented)
            {
                Payload.SerializeToFile(_filePath);
            }
            _versionIncremented = false;
        }
        public void RegisterProtocol(BaseProtocol protocol)
        {
            _dirtyPropsByProtocol[protocol] = new List<DirtyInfo>();
            //不向主服务器发送初始化SO信息
            if (protocol is OutboundClusterProtocol) return;
            DirtyInfo di;
            	//1. Clear
            di.PropertyName = "SOT_SC_CLEAR_DAT" + protocol.Id;
            di.Type = Defines.SOT_SC_CLEAR_DATA;
            _dirtyPropsByProtocol[protocol].Add(di);
            	//2. Initial
            di.PropertyName = "SOT_SC_INITIAL_DAT" + protocol.Id;
            di.Type = Defines.SOT_SC_INITIAL_DATA;
            _dirtyPropsByProtocol[protocol].Add(di);
                //3. Mark all properties as updated
            if(Payload.Count>0)
                _dirtyPropsByProtocol[protocol].AddRange(Payload.Children.Select(x => new DirtyInfo { PropertyName = x.Key, Type = Defines.SOT_SC_UPDATE_DATA }));
            //foreach (var key in Payload.Children.Keys)
            //{
            //    di.PropertyName = key;
            //    di.Type = Defines.SOT_SC_UPDATE_DATA;
            //    _dirtyPropsByProtocol[protocolId].Add(di);
            //}
        }

        public bool UnRegisterProtocol(BaseProtocol protocol)
        {
            List<DirtyInfo> dirtyInfo;
            return _dirtyPropsByProtocol.TryRemove(protocol, out dirtyInfo);
        }
        public Variant this[string key]
        {
            get { return Payload[key]; }
            set
            {
                if (value == null) UnSet(key);
                else Set(key, value);
            }
        }
      
        public Variant Set(string key, Variant value, BaseProtocol pFrom = null)
        {
            if (value == null || value == VariantType.Null)
            {
                UnSet(key);
                return value;
            }
            if (!_versionIncremented)
            {
                Version++;
                _versionIncremented = true;
            }
            Payload[key] = value;
            var updateDirtyInfo = new DirtyInfo {PropertyName = key, Type = Defines.SOT_SC_UPDATE_DATA};
            Synchronization?.Invoke(updateDirtyInfo);
#if PARALLEL
            _dirtyPropsByProtocol.AsParallel().ForAll(x => x.Value.Add(new DirtyInfo { PropertyName = key, Type =x.Key == protocolId ? Defines.SOT_SC_UPDATE_DATACK : Defines.SOT_SC_UPDATE_DATA} ));
#else

            foreach (var registeredProtocol in _dirtyPropsByProtocol)
            {
                if (registeredProtocol.Key == pFrom)
                {
                    if (pFrom is BaseClusterProtocol)continue;
                    registeredProtocol.Value.Add(new DirtyInfo
                    {
                        PropertyName = key,
                        Type = Defines.SOT_SC_UPDATE_DATA_ACK
                    });
                }
                else
                {
                    registeredProtocol.Value.Add(updateDirtyInfo);
                }
            }
#endif
            return Payload[key];
        }

        public void UnSet(string key, BaseProtocol pFrom = null)
        {
            if (!_versionIncremented)
            {
                Version++;
                _versionIncremented = true;
            }
            Payload[key] = null;
            var deleteDirtyInfo = new DirtyInfo {PropertyName = key, Type = Defines.SOT_SC_DELETE_DATA};
            Synchronization?.Invoke(deleteDirtyInfo);
#if PARALLEL
            _dirtyPropsByProtocol.AsParallel().ForAll(x => x.Value.Add(new DirtyInfo { PropertyName = key, Type = Defines.SOT_SC_DELETE_DATA }));
#else
            if (pFrom is BaseClusterProtocol)
            {
                foreach (var registeredProtocol in _dirtyPropsByProtocol.Where(x => x.Key != pFrom))
                {
                    registeredProtocol.Value.Add(deleteDirtyInfo);
                }
            }else
                foreach (var registeredProtocol in _dirtyPropsByProtocol)
                {
                     registeredProtocol.Value.Add(deleteDirtyInfo);
                }
#endif
        }

        public void Clear(BaseProtocol pFrom = null)
        {
            Payload.SetValue();
            var clearDirtyInfo = new DirtyInfo {PropertyName = null, Type = Defines.SOT_SC_CLEAR_DATA};
            Synchronization?.Invoke(clearDirtyInfo);
            foreach (var registeredProtocol in _dirtyPropsByProtocol.Where(x => x.Key != pFrom))
            {
                registeredProtocol.Value.Add(clearDirtyInfo);
            }
        }
    }
}
