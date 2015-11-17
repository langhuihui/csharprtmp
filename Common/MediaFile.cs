using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CSharpRTMP.Common
{
    public class MediaFile : IDisposable
    {
        public readonly FileInfo FileInfo;
        public H2NBinaryWriter Bw;
        public N2HBinaryReader Br;
        public Stream DataStream;
        public int UseCount;
        public MediaFile(string path)
        {
            FileInfo = new FileInfo(path);
        }
        public string FilePath => FileInfo.FullName;
        public long Length => DataStream.Length;
        public long Position { get { return DataStream.Position; } set { DataStream.Position = value; } }

        public static MediaFile Initialize(string path, FileMode fileMode = FileMode.Open,FileAccess fileAccess = FileAccess.Read)
        {
            try
            {
                var dataStream = new FileStream(path, fileMode, fileAccess);
                return new MediaFile(path) { DataStream = dataStream,Br = fileAccess!=FileAccess.Write?new N2HBinaryReader(dataStream):null,Bw = fileAccess!=FileAccess.Read?new H2NBinaryWriter(dataStream) : null};
            }
            catch (Exception ex)
            {
                Logger.FATAL("{0}",ex);
                return null;
            }
        }

        public static MediaFile CacheMediaFile(string path, FileAccess fileAccess = FileAccess.Read)
        {
            try
            {
                var fileStream = new FileStream(path, FileMode.Open, fileAccess);
                var dataStream = new MemoryStream();
                fileStream.CopyTo(dataStream);
                dataStream.Position = 0;
                fileStream.Dispose();
                return new MediaFile(path) { DataStream = dataStream, Br = fileAccess != FileAccess.Write ? new N2HBinaryReader(dataStream) : null, Bw = fileAccess != FileAccess.Read ? new H2NBinaryWriter(dataStream) : null };
            }
            catch (Exception ex)
            {
                Logger.FATAL("{0}", ex);
                return null;
            }
        }

        public void WriteFlvHead()
        {
            //2. Write FLV header
            Bw.Write(Encoding.ASCII.GetBytes("FLV"));
            //3. Write FLV version
            Bw.Write((byte)1);
            //4. Write FLV flags
            Bw.Write((byte)5);
            //5. Write FLV offset
            Bw.Write(9);
            //前一个tag长度，第一个tag永远是0
            Bw.Write(0);
            //6. Write first dummy audio
            WriteFlvTag(null, 0, true);
            //7. Write first dummy video
            WriteFlvTag(null, 0, false);
        }

        public void WriteFlvTag(MemoryStream pData,int timestamp,bool isAudio)
        {
            lock (this)
            {
                var totalLength = (int)(pData?.Length ?? 0);
                Bw.Write(isAudio ? (byte)8 : (byte)9);
                Bw.Write24(totalLength);
                Bw.WriteS32(timestamp);
                Bw.Write24(0);
                if (totalLength > 0)
                {
                    pData.WriteTo(DataStream);
                    pData.SetLength(0);
                }
                Bw.Write(totalLength + 11);
            }
        }
        public bool ReadBuffer(byte[] pBuffer, int offset = 0, int count = 0)
        {
            try
            {
                DataStream.Read(pBuffer, offset, count == 0 ? pBuffer.Length - offset : count);
            }
            catch (Exception ex)
            {
                Logger.FATAL("can't read buffer from {0} {1}", FileInfo.Name, ex.Message);
                return false;
            }
            return true;
        }
      

        public bool SeekTo(long p)
        {
            try
            {
                DataStream.Seek(p, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                Logger.FATAL($"Unable to seek to position {p} {ex.Message}");
                return false;
            }
            return true;
        }

        public bool SeekBegin()
        {
            return SeekTo(0);
        }

        public bool SeekAhead(long count)
        {
            if (count < 0)
            {
                Logger.FATAL("Invali count");
                return false;
            }

            if (count + DataStream.Position > DataStream.Length)
            {
                Logger.FATAL("End of file will be reached");
                return false;
            }
            try
            {
                DataStream.Seek(count, SeekOrigin.Current);
            }
            catch (Exception ex)
            {
                Logger.FATAL("Unable to seek ahead {0} bytes {1}", count, ex.Message);
                return false;
            }
            return true;

        }

        public bool ReadUInt8(out byte result)
        {
            try
            {
                result = (byte)DataStream.ReadByte();
            }
            catch (Exception ex)
            {
                result = 0;
                return false;
            }
            return true;
        }


        public bool ReadInt24(out int result, bool networkOrder = true)
        {
            var buffer = new byte[4];
            if (!ReadBuffer(buffer, 0, 3))
            {
                result = 0;
                return false;
            }
            if (networkOrder)
            {
                result = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(buffer, 0)) >> 8;
            }
            else
            {
                result = BitConverter.ToInt32(buffer, 0);
                result = (result << 8) >> 8;
            }
            return true;
        }

        public bool PeekByte(out byte b) => ReadUInt8(out b) && SeekBehind(1);

        public bool SeekBehind(long count)
        {
            if (count < 0)
            {
                Logger.FATAL("Invali count");
                return false;
            }

            if (DataStream.Position < count)
            {
                Logger.FATAL("End of file will be reached");
                return false;
            }
            try
            {
                DataStream.Seek(-1, SeekOrigin.Current);
            }
            catch (Exception ex)
            {
                Logger.FATAL("Unable to seek behind {0} bytes {1}", count, ex.Message);
                return false;
            }

            return true;
        }

        public bool PeekUInt64(out ulong result)
        {
            result = Br.ReadUInt64();
            return SeekBehind(8);
        }

        public bool PeekUInt16(out ushort word)
        {
            word = Br.ReadUInt16();
            return SeekBehind(2);
        }

        public bool PeekBuffer(byte[] pBuffer, int count) => ReadBuffer(pBuffer, 0, count) && SeekBehind(count);

        public void Dispose()
        {
            Br?.Dispose();
            Bw?.Dispose();
        }
    }
}
