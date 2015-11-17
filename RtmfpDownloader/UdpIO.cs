using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using CSharpRTMP.Common;
using CSharpRTMP.Core.NetIO;

namespace RtmfpDownloader
{
    public class UdpIO
    {
        public Socket Socket;
        private bool _readEnabled;
        public InputStream InputBuffer = new InputStream();
        public event Action<IPEndPoint> ReceiveData;
        public UdpIO()
        {
            Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);   
        }

        public bool ReadEnabled
        {
            get { return _readEnabled; }
            set
            {
                if (!_readEnabled && value)
                {
                    SocketAsyncEventArgs saea;
                    if (!GlobalPool<SocketAsyncEventArgs>.GetObject(out saea))
                        saea.Completed += (s, e) => OnEvent(e);
                    SetReceiveBuffer(InputBuffer, saea, 65535);
                    saea.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    // Protocol.InputBuffer.SetReceiveBuffer(saea, 65535);
                    if (!Socket.ReceiveFromAsync(saea) && !OnEvent(saea))
                    {
                    }
                }
                _readEnabled = value;
            }
        }
        public bool OnEvent(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.ReceiveFrom:
                        if (e.BytesTransferred > 0)
                        {
                            //Debug.WriteLine("receiveFrom:"+e.BytesTransferred);
                        }
                        else
                        {
                            
                            return false;
                        }
                        InputBuffer.Published += (uint)e.BytesTransferred;
                        ReceiveData?.Invoke(e.RemoteEndPoint as IPEndPoint);
                        if (_readEnabled)
                        {
                            SetReceiveBuffer(InputBuffer, e, 65535);
                            // Protocol.InputBuffer.SetReceiveBuffer(e, 65535);
                            if (!Socket.ReceiveFromAsync(e) && !OnEvent(e))
                            {
                            }
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
        public bool SetReceiveBuffer(InputStream ms, SocketAsyncEventArgs socketAsyncEventArgs, int size)
        {
            lock (ms)
            {
                ms.SetLength(ms.Published + size);
                socketAsyncEventArgs.SetBuffer(ms.GetBuffer(), (int)ms.Published, size);
            }
            return true;
        }
        public bool SignalOutputData(EndPoint address, MemoryStream outputStream)
        {
            var outputBuffer = new BufferWithOffset(outputStream);
            while (outputBuffer.Length > 0)
            {
                var sendCount = Socket.SendTo(outputBuffer.Buffer,
                    outputBuffer.Offset,
                    outputBuffer.Length, SocketFlags.None, address);
                if (sendCount < 0)
                {
                    break;
                }
                outputBuffer.Offset += sendCount;
            }
            outputStream.SetLength(0);
            return true;
        }
    }
}
