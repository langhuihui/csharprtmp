using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpRTMP.Common;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.Protocols
{
    public abstract class BaseProtocolFactory
    {
        private static uint _idGenerator;
        public readonly uint Id;

        protected BaseProtocolFactory()
        {
            Id = ++_idGenerator;
        }

        public abstract HashSet<ulong> HandledProtocols { get; }
        public abstract HashSet<string> HandledProtocolChains { get; }
        public abstract List<ulong> ResolveProtocolChain(string name);
        public abstract BaseProtocol SpawnProtocol(ulong type, Variant parameters);
    }
}
