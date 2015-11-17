using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.Protocols.Rtmp;
using CSharpRTMP.Common;
using CSharpRTMP.Core.Protocols;

namespace CSharpRTMP.Core.Streaming
{
    public class StreamsManager
    {
        public readonly BaseClientApplication Application;
        public readonly Dictionary<uint, Dictionary<uint, IStream>> StreamsByProtocolId = new Dictionary<uint, Dictionary<uint, IStream>>();
        public readonly Dictionary<ulong, Dictionary<uint, IStream>> StreamsByType = new Dictionary<ulong, Dictionary<uint, IStream>>();
        public readonly Dictionary<string, Dictionary<uint, IStream>> StreamsByName = new Dictionary<string, Dictionary<uint, IStream>>();
        public readonly Dictionary<uint, IStream> StreamsByUniqueId = new Dictionary<uint, IStream>();
        private uint _uniqueIdGenerator;

        public StreamsManager(BaseClientApplication pApplication)
        {
            _uniqueIdGenerator = 1;
            Application = pApplication;
        }

        public uint GenerateUniqueId()
        {
            return _uniqueIdGenerator++;
        }
        public bool RegisterStream(IStream stream)
        {
            if (StreamsByUniqueId.ContainsKey(stream.UniqueId))
            {
                Logger.FATAL("Stream with unique ID {0} already registered",stream.UniqueId);
                return false;
            }
            StreamsByUniqueId[stream.UniqueId] = stream;
            var protocol = stream.GetProtocol();
            if (protocol != null)
            {
                if (!StreamsByProtocolId.ContainsKey(protocol.Id))
                    StreamsByProtocolId[protocol.Id] = new Dictionary<uint, IStream>();
                StreamsByProtocolId[protocol.Id][stream.UniqueId] = stream;

            }
            if (!StreamsByType.ContainsKey(stream.Type))
            {
                StreamsByType[stream.Type] = new Dictionary<uint, IStream>();
            }
            StreamsByType[stream.Type][stream.UniqueId] = stream;
            if (!StreamsByName.ContainsKey(stream.Name))
            {
                StreamsByName[stream.Name] = new Dictionary<uint, IStream>();
            }
            StreamsByName[stream.Name][stream.UniqueId] = stream;
            Application.SignalStreamRegistered(stream);
            return true;
        }

        public void UnRegisterStream(IStream stream)
        {
            bool signalStreamUnregistered = StreamsByUniqueId.ContainsKey(stream.UniqueId);
            StreamsByUniqueId.Map_Erase(stream.UniqueId);
            var protocol = stream.GetProtocol();
            if (protocol != null)
            {
                StreamsByProtocolId.Map_Erase2(protocol.Id, stream.UniqueId);
            }
            StreamsByType.Map_Erase2(stream.Type,stream.UniqueId);
            StreamsByName.Map_Erase2(stream.Name,stream.UniqueId);
            if(signalStreamUnregistered)
                Application.SignalStreamUnRegistered(stream);
            
        }

        public void UnRegisterStreams(uint protocolId)
        {
#if PARALLEL
            FindByProtocolId(protocolId).AsParallel().ForAll(x=>UnRegisterStream(x.Value));
#else
            foreach (var baseStream in FindByProtocolId(protocolId).Values.ToArray())
                UnRegisterStream(baseStream);
#endif
        }

        public bool StreamNameAvailable(string streamName)
        {
            return Application.AllowDuplicateInboundNetworkStreams ||
                   FindByTypeByName(StreamTypes.ST_IN_NET, streamName, true, false).Count == 0;
        }

        public IEnumerable<IOutStream> GetWaitingSubscribers(string streamName, ulong inboundStreamType)
        {
            if (((inboundStreamType) & StreamTypes.ST_IN.GetTagMask()) != StreamTypes.ST_IN)
                return new List<IOutStream>();
            var shortName = streamName.Split('?')[0];
            return FindByTypeByName(StreamTypes.ST_OUT, shortName, true, false)
                .Concat(FindByTypeByName(StreamTypes.ST_OUT, streamName, true, false))
                .Select(x => x.Value).OfType<IOutStream>()
                .Where(
                    x => !x.IsLinked && x.IsCompatibleWithType(inboundStreamType));
        }
        public Dictionary<uint, IStream> FindByName(string name, bool partial)
        {
            if (!partial)
                return StreamsByName.ContainsKey(name) ? StreamsByName[name] : new Dictionary<uint, IStream>();
            return StreamsByName.Where(x => x.Key.Contains(name))
                .SelectMany(x => x.Value)
                .ToDictionary(x => x.Key, x => x.Value);
        }
        public Dictionary<uint, IStream> FindByType(ulong type, bool partialType)
        {
            if (!partialType)
                return StreamsByType.ContainsKey(type) ? StreamsByType[type] : new Dictionary<uint, IStream>();
            return
                StreamsByType.Where(x => (x.Key & type.GetTagMask()) == type)
                    .SelectMany(x => x.Value)
                    .ToDictionary(x => x.Key, x => x.Value);
        }
        public Dictionary<uint, IStream> FindByTypeByName(ulong type, string name, bool partialType, bool partialName)
        {
            return FindByType(type, partialType)
                .Where(x => partialName ? x.Value.Name.Contains(name) : x.Value.Name == name)
                .ToDictionary(x => x.Key, x => x.Value);
        }
        public Dictionary<uint, IStream> FindByProtocolIdByType(uint protocolId, ulong type, bool partial)
        {
            var mask = partial ? type.GetTagMask() : 0xffffffffffffffffL;
            return FindByProtocolId(protocolId)
                .Where(x => (x.Value.Type & mask) == type)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        public Dictionary<uint, IStream> FindByProtocolId(uint id)
        {
            return StreamsByProtocolId.ContainsKey(id) ? StreamsByProtocolId[id] : new Dictionary<uint, IStream>();
        }

        public Dictionary<uint, IStream> FindByProtocolIdByName(uint protocolId,
            string name, bool partial)
        {
            return
                FindByProtocolId(protocolId)
                    .Where(x => partial ? x.Value.Name.Contains(name) : x.Value.Name == name)
                    .ToDictionary(x => x.Key, x => x.Value);
        }

        public Dictionary<uint, IStream> FindByProtocolIdByTypeByName(uint protocolId, ulong type, string name,
            bool partialType, bool partialName)
        {
            return FindByProtocolId(protocolId)
                .Where(
                    x =>
                        (x.Value.Type & (partialType ? type.GetTagMask() : 0xffffffffffffffffL)) == type &&
                        (partialName ? x.Value.Name.Contains(name) : x.Value.Name == name))
                .ToDictionary(x => x.Key, x => x.Value);
        }
        public IStream FindByUniqueId(uint uniqueId)
        {
            return StreamsByUniqueId.ContainsKey(uniqueId) ? StreamsByUniqueId[uniqueId] : null;
        }

        public IOutFileStream CreateOutFileStream(BaseProtocol protocol, IInStream instream, bool append)
        {
            var pOutFileStream = CreateOutFileStream(protocol, instream.Name,null, append);
            pOutFileStream?.Link(instream);
            return pOutFileStream;
        }
        public IOutFileStream CreateOutFileStream(BaseProtocol protocol,string name,string filePath,bool append)
        {
            var meta = GetMetaData(name, false,protocol.Application.Configuration);
            string fileName = meta[Defines.META_SERVER_MEDIA_DIR];
            fileName += meta[Defines.META_SERVER_FILE_NAME];
            this.Log().Info("fileName: {0}", fileName);
            IOutFileStream pOutFileStream = null;
            switch ((string)meta[Defines.META_MEDIA_TYPE])
            {
                case Defines.MEDIA_TYPE_FLV:
                case Defines.MEDIA_TYPE_LIVE_OR_FLV:
                    if (append)
                    {
                        //删除原来的辅助文件
                        var seekPath = meta[Defines.META_SERVER_FULL_PATH] + "." + Defines.MEDIA_TYPE_SEEK;
                        var metaDataPath = meta[Defines.META_SERVER_FULL_PATH] + "." + Defines.MEDIA_TYPE_META;
                        File.Delete(seekPath);
                        File.Delete(metaDataPath);
                    }
                    pOutFileStream = new OutFileRTMPFLVStream(protocol, Application.StreamsManager, filePath??fileName, name) { Appending = append };
                    break;
                case Defines.MEDIA_TYPE_MP4:
                    Logger.FATAL("Streaming to MP4 file not supported");
                    break;
                default:
                    Logger.FATAL("Media type not supported");
                    break;
            }
           
            return pOutFileStream;
        }
        public Variant GetMetaData(string streamName, bool extractInnerMetadata, Variant configuration)
        {
            bool keyframeSeek = configuration[Defines.CONF_APPLICATION_KEYFRAMESEEK];
            int clientSideBuffer = configuration[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER];
            var seekGranularity = (uint)((double)configuration[Defines.CONF_APPLICATION_SEEKGRANULARITY] * 1000);
            bool renameBadFiles = configuration[Defines.CONF_APPLICATION_RENAMEBADFILES];
            bool externSeekGenerator = configuration[Defines.CONF_APPLICATION_EXTERNSEEKGENERATOR];
            var result = Variant.Get();
            result[Defines.META_REQUESTED_STREAM_NAME] = streamName;
            result[Defines.CONF_APPLICATION_KEYFRAMESEEK] = keyframeSeek;
            result[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER] = clientSideBuffer;
            result[Defines.CONF_APPLICATION_SEEKGRANULARITY] = seekGranularity;
            result[Defines.CONF_APPLICATION_RENAMEBADFILES] = renameBadFiles;
            result[Defines.CONF_APPLICATION_EXTERNSEEKGENERATOR] = externSeekGenerator;
            var parts = streamName.Split(':');
            if (parts.Length != 1 && parts.Length != 2 && parts.Length != 5)
            {
                Logger.FATAL("Invalid stream name format:{0}", streamName);
                return result;
            }
            result[Defines.META_MEDIA_TYPE] = parts.Length == 1 ? Defines.MEDIA_TYPE_LIVE_OR_FLV : parts[0].ToLower();
            var searchFor = "";
            switch ((string)result[Defines.META_MEDIA_TYPE])
            {
                case Defines.MEDIA_TYPE_LIVE_OR_FLV:
                    searchFor = parts[0] + ".flv";
                    break;
                case Defines.MEDIA_TYPE_MP3:
                    searchFor = parts[1] + ".mp3";
                    break;
                default:
                    searchFor = parts[1];
                    break;
            }
            result[Defines.META_SERVER_FILE_NAME] = searchFor;
            var _mediaFolder = Application.MediaPath;
            result[Defines.META_SERVER_MEDIA_DIR] = _mediaFolder;

            result[Defines.META_SERVER_FULL_PATH] = searchFor[0] == Path.DirectorySeparatorChar
                ? (searchFor.StartsWith(_mediaFolder.NormalizePath())
                    ? searchFor
                    : "")
                : _mediaFolder.NormalizePath(searchFor);
            if (string.IsNullOrEmpty(result[Defines.META_SERVER_FULL_PATH])) return result;
            var metaPath = result[Defines.META_SERVER_FULL_PATH] + "." + Defines.MEDIA_TYPE_META;
            var seekPath = result[Defines.META_SERVER_FULL_PATH] + "." + Defines.MEDIA_TYPE_SEEK;
            var regenerateFiles = true;
            if (File.Exists(metaPath) && File.Exists(seekPath))
            {
                var capabilities = new StreamCapabilities();
                var originalServerFullPath = (string)result[Defines.META_SERVER_FULL_PATH];
                regenerateFiles =
                (new FileInfo(metaPath).LastWriteTime < new FileInfo((string)result[Defines.META_SERVER_FULL_PATH]).LastWriteTime)
                || (new FileInfo(seekPath).LastWriteTime < new FileInfo((string)result[Defines.META_SERVER_FULL_PATH]).LastWriteTime)
                || !Variant.DeserializeFromFile(metaPath, out result)
                || (!StreamCapabilities.Deserialize(seekPath, capabilities));
                regenerateFiles |=
                        (result[Defines.META_SERVER_FULL_PATH] == null)
                        || ((string)result[Defines.META_SERVER_FULL_PATH] != originalServerFullPath)
                        || (result[Defines.CONF_APPLICATION_KEYFRAMESEEK] == null)
                        || ((bool)result[Defines.CONF_APPLICATION_KEYFRAMESEEK] != keyframeSeek)
                        || (result[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER] == null)
                        || ((int)result[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER] != clientSideBuffer)
                        || (result[Defines.CONF_APPLICATION_SEEKGRANULARITY] == null)
                        || ((uint)result[Defines.CONF_APPLICATION_SEEKGRANULARITY] != seekGranularity);
                if (regenerateFiles)
                {
                    result[Defines.META_SERVER_FULL_PATH] = originalServerFullPath;
                    result[Defines.CONF_APPLICATION_KEYFRAMESEEK] = keyframeSeek;
                    result[Defines.CONF_APPLICATION_CLIENTSIDEBUFFER] = clientSideBuffer;
                    result[Defines.CONF_APPLICATION_SEEKGRANULARITY] = seekGranularity;
                }
            }
            if (!regenerateFiles)
            {
                result[Defines.META_REQUESTED_STREAM_NAME] = streamName;
                return result;
            }
            this.Log().Info("Generate seek/meta for file {0}", result[Defines.META_SERVER_FULL_PATH]);
            //8. We either have a bad meta file or we don't have it at all. Build it
            if (extractInnerMetadata)
            {
                if (!BaseInFileStream.ResolveCompleteMetadata(ref result))
                {
                    Logger.FATAL("Unable to get metadata. Partial result:\n{0}",
                            result);
                    return Variant.Get();
                }
            }
            result.SerializeToFile(metaPath);
            return result;
        }
    }
}
