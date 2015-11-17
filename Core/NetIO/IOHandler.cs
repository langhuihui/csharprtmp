using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;

namespace CSharpRTMP.Core.NetIO
{
	public enum IOHandlerType {
		IOHT_ACCEPTOR,
		IOHT_TCP_CONNECTOR,
		IOHT_TCP_CARRIER,
		IOHT_UDP_CARRIER,
		IOHT_INBOUNDNAMEDPIPE_CARRIER,
		IOHT_TIMER,
		IOHT_STDIO
    }

	public abstract class IOHandler:IDisposable
	{
        public static uint _idGenerator;
	    public readonly IOHandlerType Type;
	    public readonly uint Id;
        public Socket Socket;
        protected IOHandler( IOHandlerType type)
        {
            Type = type;
            Id = ++_idGenerator;
            this.RegisterIOHandler();
        }
	    public virtual bool ReadEnabled { get; set; }
        public virtual bool WriteEnabled { get; set; }
        public virtual bool AcceptEnabled { get; set; }
	    public BaseProtocol Protocol;

        public virtual bool SignalOutputData(MemoryStream outputStream = null)
	    {
            Logger.WARN("Should be overrided");
            return true;
	    }
        public virtual bool SignalOutputData(EndPoint address, MemoryStream outputStream)
        {
            Logger.WARN("Should be overrided");
            return true;
        }
        public virtual bool SignalOutputData(EndPoint address)
	    {
            Logger.WARN("Should be overrided");
	        return true;
	    }
        public abstract bool OnEvent(SocketAsyncEventArgs e);

        public virtual void Dispose()
	    {
	        if (Protocol != null) {
		        Protocol.IOHandler = null;
		        Protocol.EnqueueForDelete();
		        Protocol = null;
	        }
	        this.UnRegisterIOHandler();
	    }

	    public abstract void GetStats(Variant variant, uint namespaceId);

	}
}

