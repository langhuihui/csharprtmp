using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    public static class ProtocolFactoryManager
    {
        private static readonly Dictionary<uint, BaseProtocolFactory> _factoriesById = new Dictionary<uint, BaseProtocolFactory>();
        private static readonly Dictionary<ulong, BaseProtocolFactory> _factoriesByProtocolId = new Dictionary<ulong, BaseProtocolFactory>();
        private static readonly Dictionary<string, BaseProtocolFactory> _factoriesByChainName = new Dictionary<string, BaseProtocolFactory>();

        public static bool RegisterProtocolFactory(this BaseProtocolFactory pFactory)
        {//1. Test to see if this factory is already registered
            if (_factoriesById.ContainsKey(pFactory.Id))
            {
                Logger.FATAL("Factory id {0} already registered", pFactory.Id);
                return false;
            }
            //2. Test to see if the protocol chains exported by this factory are already in use
            if (pFactory.HandledProtocolChains.Any(x => _factoriesByChainName.ContainsKey(x)))
            {
                Logger.FATAL("protocol chain  already handled by factory ");
                return false;
            }
            //3. Test to see if the protocols exported by this factory are already in use
            if (pFactory.HandledProtocols.Any(x => _factoriesByProtocolId.ContainsKey(x)))
            {
                Logger.FATAL("protocol  already handled by factory ");
                return false;
            }
            //4. Register everything
#if PARALLEL
            pFactory.HandledProtocolChains().AsParallel().ForAll(x => _factoriesByChainName[x]=pFactory);
            pFactory.HandledProtocols().AsParallel().ForAll(x => _factoriesByProtocolId[x]=pFactory);
#else
            foreach (var handledProtocolChain in pFactory.HandledProtocolChains)
            {
                _factoriesByChainName[handledProtocolChain] = pFactory;
            }
            foreach (var handledProtocol in pFactory.HandledProtocols)
            {
                _factoriesByProtocolId[handledProtocol] = pFactory;
            }
#endif
            _factoriesById[pFactory.Id] = pFactory;
            return true;
        }

        public static bool UnRegisterProtocolFactory(uint factoryId)
        {
            BaseProtocolFactory f;
            if (_factoriesById.TryGetValue(factoryId, out f))
            {
                return UnRegisterProtocolFactory(f);
            }
            Logger.WARN("Factory id not found: {0}", factoryId);
            return true;
        }

        public static bool UnRegisterProtocolFactory(BaseProtocolFactory pFactory)
        {
            if (pFactory == null)
            {
                Logger.WARN("pFactory is null");
                return true;
            }
            if (!_factoriesById.ContainsKey(pFactory.Id))
            {
                Logger.WARN("Factory id not found: {0}",pFactory.Id);
                return true;
            }
            pFactory.HandledProtocolChains.AsParallel().ForAll(x => _factoriesByChainName.Remove(x));
            pFactory.HandledProtocols.AsParallel().ForAll(x => _factoriesByProtocolId.Remove(x));
            _factoriesById.Remove(pFactory.Id);
            return true;
        }

        public static List<ulong> ResolveProtocolChain(string name)
        {
            BaseProtocolFactory f;
            if (_factoriesByChainName.TryGetValue(name, out f))
            {
                return f.ResolveProtocolChain(name);
            }
            Logger.FATAL("chain {0} not registered by any protocol factory",name);
            return new List<ulong>();
        }

        public static BaseProtocol CreateProtocolChain(string name, Variant parameters)
        {
            var chain = ResolveProtocolChain(name);
            if (chain.Any()) return CreateProtocolChain(chain, parameters);
            Logger.FATAL("Unable to create protocol chain");
            return null;
        }

        public static BaseProtocol CreateProtocolChain(List<ulong> chain, Variant parameters)
        {
            BaseProtocol pResult = null;
            //1. Check and see if all the protocols are handled by a factory
            if (chain.Any(x => !_factoriesByProtocolId.ContainsKey(x)))
            {
                Logger.FATAL("protocol not handled by anynone");
                return null;
            }
            //2. Spawn the protocols
    
            foreach (var item in chain)
            {
                var pProtocol = _factoriesByProtocolId[item].SpawnProtocol(item, parameters);
                if (pProtocol == null)
                {
                    Logger.FATAL("Unable to spawn protocol {0} handled by factory {1}", item.TagToString(), _factoriesByProtocolId[item].Id);
                    pResult?.FarEndpoint.Dispose();
                    return null;
                }
                if (pResult != null)
                    pResult.NearProtocol = pProtocol;
                pResult = pProtocol;
            }
            return pResult.NearEndpoint;
            
        }

        public static string Dump()
        {
            var result = "Factories by id" + Environment.NewLine;
            result += string.Join(Environment.NewLine, _factoriesById.Select(x => "\t" + x.Key + "\t" + x.Value));
            result +=Environment.NewLine+ "Factories by protocol id" + Environment.NewLine;
            result += string.Join(Environment.NewLine,
                _factoriesByProtocolId.Select(x => "\t" + x.Key.TagToString() + "\t" + x.Value));
            result += "Factories by chain name" + Environment.NewLine;
            result += string.Join(Environment.NewLine,
               _factoriesByChainName.Select(x => "\t" + x.Key + "\t" + x.Value));
            return result + Environment.NewLine;
        }
    }
}
