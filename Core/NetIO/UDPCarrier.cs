using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;
using Newtonsoft.Json.Linq;

namespace CSharpRTMP.Core.NetIO
{
    public class UDPCarrier : IOHandler
    {
        public string NearIP;
        public int NearPort;
        public int Rx;
        public Variant Parameters;
       
        public UDPCarrier(Socket socket)
            : base(IOHandlerType.IOHT_UDP_CARRIER)
        {
            Socket = socket;
            var nearInfo = socket.LocalEndPoint as IPEndPoint;
            NearIP = nearInfo?.Address?.ToString();
            NearPort = nearInfo?.Port??0;
        }

        public bool GetStats(Variant info)
        {
            info.Add("type", "IOHT_UDP_CARRIER");
            info.Add("nearIP", NearIP);
            info.Add("nearPort", NearPort);
            info.Add("rx", Rx);

            return true;
        }
        public bool StartAccept()
        {
            ReadEnabled = true;
            return true;
        }

        //public override bool SignalOutputData(MemoryStream ouputStream = null)
        //{
        //    while (Protocol.OutputBuffer!=null)
        //    {
        //        var outputBuffer = Protocol.OutputBuffer;
        //        SocketError errorCode;
        //        var sendCount = OutboundFd.Send(outputBuffer.GetBuffer(),
        //            (int)outputBuffer.Consumed,
        //            (int)outputBuffer.Length, SocketFlags.None, out errorCode);
        //        if (errorCode!=SocketError.Success || sendCount <= 0)
        //        {
        //            Logger.FATAL("Unable to send data.{0}:{1}", NearIP, NearPort);
        //            IOHandlerManager.EnqueueForDelete(this);
        //            break;
        //        }
        //        outputBuffer.Ignore((uint)sendCount);
        //    }
        //    //Protocol.OutputBuffer.Recycle(true);
        //    return true;
        //}
        public override bool SignalOutputData(EndPoint address, MemoryStream outputStream)
        {
            var outputBuffer = new BufferWithOffset(outputStream);
            while (outputBuffer.Length>0)
            {
                var sendCount = Socket.SendTo(outputBuffer.Buffer,
                    outputBuffer.Offset,
                    outputBuffer.Length, SocketFlags.None, address);
                if (sendCount < 0)
                {
                    Logger.FATAL("Unable to send data.{0}:{1}", NearIP, NearPort);
                    IOHandlerManager.EnqueueForDelete(this);
                    break;
                }
                outputBuffer.Offset += sendCount;
            }
            outputStream.SetLength(0);
            return true;
        }
        //public override bool SignalOutputData(EndPoint address)
        //{
        //    var outputBuffer = Protocol.OutputBuffer;
        //    while (outputBuffer.Length > 0)
        //    {
        //        var sendCount = OutboundFd.SendTo(outputBuffer.GetBuffer(),
        //            (int)outputBuffer.Consumed,
        //            (int)outputBuffer.Length, SocketFlags.None, address);
        //        if (sendCount < 0)
        //        {
        //            Logger.FATAL("Unable to send data.{0}:{1}", NearIP, NearPort);
        //            IOHandlerManager.EnqueueForDelete(this);
        //            break;
        //        }
        //        outputBuffer.Ignore((uint)sendCount);
        //    }
        //   // outputBuffer.Recycle(true);
        //    return true;
        //}
        public bool SetReceiveBuffer(InputStream ms, SocketAsyncEventArgs socketAsyncEventArgs, int size)
        {
            lock (ms)
            {
                ms.SetLength(ms.Published + size);
                socketAsyncEventArgs.SetBuffer(ms.GetBuffer(), (int)ms.Published, size);
            }
            return true;
        }
        
        public override bool ReadEnabled
        {
            set
            {
                if (!base.ReadEnabled && value)
                {
                    var saea = this.CreateOrGetSocketAsyncEventArgs();
                    SetReceiveBuffer( Protocol.InputBuffer, saea, 65535);
                    saea.RemoteEndPoint = new IPEndPoint(IPAddress.Any,0);
                   // Protocol.InputBuffer.SetReceiveBuffer(saea, 65535);
                    if (!Socket.ReceiveFromAsync(saea) && !OnEvent(saea))
                        IOHandlerManager.EnqueueForDelete(this);
                }
                if(!value) Protocol?.InputBuffer?.IgnoreAll();
                base.ReadEnabled = value;
            }
        }
        public override bool OnEvent(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.ReceiveFrom:
                        if (e.BytesTransferred > 0)
                        {
                            //Debug.WriteLine("receiveFrom:"+e.BytesTransferred);
                            Rx += e.BytesTransferred;
                        }
                        else
                        {
                            Logger.WARN("socket read data error");
                            return false;
                        }
                        Protocol.InputBuffer.Published += (uint)e.BytesTransferred;
                        Protocol.SignalInputData(Protocol.InputBuffer, e.RemoteEndPoint as IPEndPoint);
                        if (ReadEnabled)
                        {
                            SetReceiveBuffer(Protocol.InputBuffer, e, 65535);
                           // Protocol.InputBuffer.SetReceiveBuffer(e, 65535);
                            if (!Socket.ReceiveFromAsync(e) && !OnEvent(e))
                                IOHandlerManager.EnqueueForDelete(this);
                        }
                        else
                        {
                            ReadEnabled = false;
                            e.ReturnPool();
                        }
                        break;
                    case SocketAsyncOperation.Receive:
                        return false;
                    case SocketAsyncOperation.SendTo:
                        return false;
                }
                return true;
            }
            Logger.WARN(e.SocketError.ToString());
            return false;
        }

        public override void GetStats(Variant info, uint namespaceId)
        {
            if (!GetEndpointsInfo())
            {
                Logger.FATAL("Unable to get endpoints info");
                info.SetValue("unable to get endpoints info");
                return;
            }
            info.Add("type", "IOHT_UDP_CARRIER");
            info.Add("nearIP",NearIP);
            info.Add("nearPort",NearPort);
            info.Add("rx",Rx);
        }

        private bool GetEndpointsInfo()
        {
            var nearInfo = Socket.LocalEndPoint as IPEndPoint;
            NearIP = nearInfo.Address.ToString();
            NearPort = nearInfo.Port;
            return true;
        }

        public static UDPCarrier Create(string bindIp, int bindPort)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //3. bind if necessary
            if (bindIp != "")
            {
                socket.Bind(new IPEndPoint(IPAddress.Parse(bindIp), bindPort));
            }
            
            //4. Create the carrier
            var pResult = new UDPCarrier(socket);
            return pResult;
        }
        public static UDPCarrier Create(string bindIp, ushort bindPort,BaseProtocol pProtocol)
        {
            if (pProtocol == null)
            {
                Logger.FATAL("Protocol can't be null");
                return null;
            }

            UDPCarrier pResult = Create(bindIp, bindPort);
            if (pResult == null)
            {
                Logger.FATAL("Unable to create UDP carrier");
                return null;
            }

            pResult.Protocol = pProtocol.FarEndpoint;
            pProtocol.FarEndpoint.IOHandler = pResult;

            return pResult;
        }
    }
}
