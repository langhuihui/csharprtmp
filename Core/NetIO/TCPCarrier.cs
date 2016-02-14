using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using CSharpRTMP.Common;
using Microsoft.IO;

namespace CSharpRTMP.Core.NetIO
{
    public class TCPCarrier:IOHandler
    {
        public string NearIP => NearInfo?.Address.ToString();
        public int NearPort => NearInfo?.Port??0;
        public string FarIP => FarInfo?.Address.ToString();
        public int FarPort => FarInfo?.Port ?? 0;
        public int Rx, Tx;
        public IPEndPoint NearInfo => Socket.LocalEndPoint as IPEndPoint;
        public IPEndPoint FarInfo => Socket.RemoteEndPoint as IPEndPoint;
        private bool _outputRunning;
        public TCPCarrier(Socket socket)
            : base(IOHandlerType.IOHT_TCP_CARRIER)
        {
            Socket = socket;
        }
        //void DisableWriteData()
        //{
        //    if (!WriteEnabled) return;
        //    _enableWriteDataCalled = false;
        //    Protocol.ReadyForSend();
        //    WriteEnabled = false;
        //    if (_enableWriteDataCalled)
        //    {
        //        WriteEnabled = true;
        //    }
        //}
        public override bool SignalOutputData(MemoryStream s = null)
        {
            //if (!WriteEnabled)
            //{
            //    WriteEnabled = true;
            //}
            WriteEnabled = true;
            if (s == null) s = Protocol.OutputBuffer;
            var needToSend = s.Length;
            var buffer = s.GetBuffer();
            while (Socket.Connected && needToSend > 0)
            {
                try
                {
                    var sendCount = Socket.Send(buffer,
                                (int)s.Position,
                                (int)needToSend, SocketFlags.None);
                    if (sendCount <= 0)
                    {
                        throw new Exception("sendCount<=0");
                    }
                    Tx += sendCount;
                    needToSend -= sendCount;
                    s.Position += sendCount;
                }
                catch (Exception ex)
                {
                    Logger.FATAL("Unable to send data.{0}:{1} -> {2}:{3}", FarIP, FarPort, NearIP,
                            NearPort);
                    IOHandlerManager.EnqueueForDelete(this);
                    break;
                }
            }
            s.SetLength(0);
            WriteEnabled = false;
            return true;
        }

        public bool GetStats(Variant info)
        {
            info.Add("type", "IOHT_TCP_CARRIER");
            info.Add("farIP", FarIP);
            info.Add("farPort", FarPort);
            info.Add("nearIP", NearIP);
            info.Add("nearPort", NearPort);
            info.Add("rx",Rx);
            info.Add("tx",Tx);
            return true;
        }

        //public override bool WriteEnabled
        //{
        //    set {
        //        if (!base.WriteEnabled && value && Protocol.OutputBuffer != null)
        //        {
        //            try
        //            {
        //                base.WriteEnabled = true;
        //                while (Protocol.OutputBuffer != null)
        //                {
        //                    var sendCount = OutboundFd.Send(Protocol.OutputBuffer.GetBuffer(),
        //                        (int) Protocol.OutputBuffer.Consumed,
        //                        (int) Protocol.OutputBuffer.Length, SocketFlags.None);
        //                    if (sendCount <= 0)
        //                    {
        //                        Logger.FATAL("Unable to send data.{0}:{1} -> {2}:{3}", FarIP, FarPort, NearIP,
        //                            NearPort);
        //                        IOHandlerManager.EnqueueForDelete(this);
        //                        break;
        //                    }
        //                    Tx += sendCount;
        //                    Protocol.OutputBuffer.Ignore((uint) sendCount);
        //                }
        //                base.WriteEnabled = false;
        //            }
        //            catch (Exception ex)
        //            {
        //                Logger.FATAL("{0}", ex);
        //                IOHandlerManager.EnqueueForDelete(this);
        //            }
        //        }
        //        else if (!value) base.WriteEnabled = false;
        //    }
        //}
        public override bool ReadEnabled
        {
            set
            {
                if (!base.ReadEnabled && value)
                {
                    var saea = this.CreateOrGetSocketAsyncEventArgs();
                    SetReceiveBuffer( Protocol.InputBuffer,saea);
                    if (!Socket.ReceiveAsync(saea) && !OnEvent(saea))
                        IOHandlerManager.EnqueueForDelete(this);
                }
                base.ReadEnabled = value;
            }
        }
        public override bool OnEvent(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Receive:
                        if (e.BytesTransferred > 0)
                        {
                            Rx += e.BytesTransferred;
                        }
                        else
                        {
                            Logger.WARN("socket read data error");
                            return false;
                        }
                        //Logger.INFO("rec:{0}",Rx);
                        Protocol.InputBuffer.Published += (uint)e.BytesTransferred;
                        
                        Protocol.SignalInputData(e.BytesTransferred);

                        if (ReadEnabled)
                        {
                           SetReceiveBuffer(Protocol.InputBuffer,e);
                           if (!Socket.ReceiveAsync(e) && !OnEvent(e))
                                IOHandlerManager.EnqueueForDelete(this);
                        }
                        else
                        {
                            //ReadEnabled = false;
                            e.ReturnPool();
                        }
                        break;
                    //case SocketAsyncOperation.Send:
                    //    if (Protocol.OutputBuffer == null)
                    //    {
                    //        DisableWriteData();
                    //    }
                    //    else
                    //    {
                    //        if (e.BytesTransferred < 0)
                    //        {
                    //            Logger.FATAL("Unable to send data.{0}:{1} -> {2}:{3}", FarIP, FarPort, NearIP, NearPort);
                    //            IOHandlerManager.EnqueueForDelete(this);
                    //            return false;
                    //        }
                    //        Protocol.OutputBuffer.Ignore((uint)e.BytesTransferred);
                    //       // if(e.BytesTransferred==4096)Logger.Debug("{0}", e.BytesTransferred);
                    //        Tx += e.BytesTransferred;
                    //        if (Protocol.OutputBuffer != null && WriteEnabled)
                    //        {
                    //            SetSendBuffer( Protocol.OutputBuffer,e);
                    //            if (!OutboundFd.SendAsync(e) && !OnEvent(e)) IOHandlerManager.EnqueueForDelete(this);
                    //        }
                    //        else
                    //        {
                    //            DisableWriteData();
                    //            e.ReturnPool();
                    //        }
                    //    }
                    //    break;
                }
                return true;
            }
            Logger.WARN("{1}({0}):"+e.SocketError,Id,GetType().Name);
            return false;
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            info.Add("type", "IOHT_UDP_CARRIER");
            info.Add("farIP",FarIP);
            info.Add("farPort", FarPort);
            info.Add("nearIP", NearIP);
            info.Add("nearPort", NearPort);
            info.Add("rx", Rx);
        }
        public bool SetReceiveBuffer(InputStream ms, SocketAsyncEventArgs socketAsyncEventArgs)
        {
            lock (ms)
            {
                ms.SetLength(ms.Published + 4096);
                socketAsyncEventArgs.SetBuffer(ms.GetBuffer(), (int)ms.Published, 4096);
            }
            return true;
        }
        //public void SetSendBuffer(OutputStream ms, SocketAsyncEventArgs socketAsyncEventArgs)
        //{
        //    if (ms.GetBuffer() != socketAsyncEventArgs.Buffer)
        //    {
        //        Logger.Debug("!{0}", ms.GetBuffer().Length);
        //        socketAsyncEventArgs.SetBuffer(ms.GetBuffer(), (int) ms.Consumed,
        //            Math.Min((int)ms.Length, 4096));
        //    }
        //    else
        //    {
        //        socketAsyncEventArgs.SetBuffer((int)ms.Consumed, Math.Min((int)ms.Length, socketAsyncEventArgs.Buffer.Length - (int)ms.Consumed));
        //    }
  
        //}
    }
}
