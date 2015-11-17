using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Streaming;
using static CSharpRTMP.Common.Logger;

namespace CSharpRTMP.Core.Protocols.Rtsp
{
    public static class SDP
    {
        public const string SDP_SESSION = "session";
        public const string SDP_MEDIATRACKS = "mediaTracks";
        public const string SDP_A = "attributes";
        public const string SDP_B = "bandwidth";
        public const string SDP_C = "connection";
        public const string SDP_E = "email";
        public const string SDP_I = "sessionInfo";
        public const string SDP_K = "encryptionKey";
        public const string SDP_M = "media";
        public const string SDP_O = "owner";
        public const string SDP_P = "phone";
        public const string SDP_R = "repeat";
        public const string SDP_S = "sessionName";
        public const string SDP_T = "startStopTime";
        public const string SDP_U = "descriptionUri";
        public const string SDP_V = "version";
        public const string SDP_Z = "timeZone";
        public static bool ParseSDP(Variant sdp, string raw)
        {
            sdp.SetValue();
            sdp[SDP_SESSION] = Variant.Get();
            sdp[SDP_MEDIATRACKS] = Variant.Get();
            sdp[SDP_MEDIATRACKS].IsArray = true;
            var lines = raw.Replace("\r\n", "\n").Split('\n');
            //4. Detect the media tracks indexes
            var trackIndexes = new List<uint>();
            for (uint i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("m="))
                {
                    trackIndexes.Add(i);
                }
            }
            if (trackIndexes.Count == 0)
            {
                FATAL("No tracks found");
                return false;
            }
            //5. Parse the header
            if (!ParseSection(sdp[SDP_SESSION], lines, 0, trackIndexes[0]))
            {
                FATAL("Unable to parse header");
                return false;
            }
            //6. Parse the media sections

            Variant media;
            for (var i = 0; i < trackIndexes.Count - 1; i++)
            {
                media = Variant.Get();
                
                if (!ParseSection(media, lines, trackIndexes[i], trackIndexes[i + 1] - trackIndexes[i]))
                {
                    FATAL("Unable to parse header");
                    return false;
                }
                sdp[SDP_MEDIATRACKS].Add(media);
            }

            //7. Parse the last media section
            media = Variant.Get();
            if (!ParseSection(media, lines,
                    trackIndexes[trackIndexes.Count - 1],
                    lines.Length- trackIndexes[trackIndexes.Count - 1]))
            {
                FATAL("Unable to parse header");
                return false;
            }
            sdp[SDP_MEDIATRACKS].Add(media);

            return true;
        }

        public static Variant GetVideoTrack(this Variant _this,uint index, string uri)
        {
            //1. Find the track
            Variant track = GetTrack(_this,index, "video");
            if (track == VariantType.Null)
            {
                FATAL("Video track index {0} not found", index);
                return Variant.Get();
            }
            //2. Prepare the info
            Variant result = Variant.Get();
            result["ip"] = _this[SDP_SESSION,SDP_O,"address"];
            string control = track[SDP_A,"control"];
            if (control.StartsWith("rtsp"))
                result["controlUri"]= control;
            else
                result["controlUri"] = uri + "/" + control;
            result["codec"] = track[SDP_A,"rtpmap","encodingName"];
            if (result["codec"] != (byte)VideoCodec.H264)
            {
                FATAL("The only supported video codec is h264");
                return Variant.Get();
            }
            result["h264SPS"] = track[SDP_A, "fmtp", "sprop-parameter-sets", "SPS"];
            result["h264PPS"] = track[SDP_A, "fmtp", "sprop-parameter-sets", "PPS"];
            result["globalTrackIndex"] = track["globalTrackIndex"];
            result["isAudio"] = false;

            result["bandWidth"] = track[SDP_B] != null ? (uint) track[SDP_B] : 0;

            //3. Done
            return result;
        }

        


        public static Variant GetAudioTrack(this Variant _this, uint index, string uri)
        {
            //1. Find the track
            Variant track = GetTrack(_this,index, "audio");
            if (track == VariantType.Null)
            {
                FATAL("Audio track index {0} not found", index);
                return Variant.Get();
            }
            //2. Prepare the info
            Variant result = Variant.Get();
            result["ip"] = _this[SDP_SESSION,SDP_O,"address"];
            string control = track[SDP_A,"control"];
            if (control.StartsWith("rtsp"))
                result["controlUri"] = control;
            else
                result["controlUri"] = uri + "/" + control;
            result["codec"] = Convert.ToByte((string)track[SDP_A, "rtpmap", "encodingName"]);
            result["rate"] = track[SDP_A, "rtpmap", "clockRate"];
            //if (result["codec"] != (byte)AudioCodec.Aac)
            //{
            //    FATAL("The only supported audio codec is aac");
            //    return Variant.Get();
            //}
            switch ((AudioCodec)(byte)result["codec"])
            {
                case AudioCodec.Aac:
                    result["codecSetup"] = track[SDP_A, "fmtp", "config"];
                    break;
            }
            
            result["globalTrackIndex"] = track["globalTrackIndex"];
            result["isAudio"] = true;

            result["bandWidth"] = track[SDP_B] != null ? (uint)track[SDP_B] : 0;

            //3. Done
            return result;
        }

        public static uint GetTotalBandwidth(this Variant _this)
        {
            if (_this[SDP_SESSION, SDP_B] != null)
            {
                return _this[SDP_SESSION, SDP_B];
            }
            return 0;
        }
        public static string GetStreamName(this Variant _this)
        {
            if (_this[SDP_SESSION, SDP_S] != null)
            {
                return _this[SDP_SESSION, SDP_S];
            }
            return "";
        }

        public static readonly string[] Keys = {"client_port", "server_port", "interleaved"};
        public static bool ParseTransportLine(string raw, Variant result)
        {
            //1. split after ';'
            var parts = raw.Split(';');

            //2. Construct the result
            foreach(var part in parts.Select(x=>x.Trim()).Where(x=>x!=""))
            {
                
                
                var pos = part.IndexOf('=');
                if (pos == -1)
                {
                    result[part.ToLower()] = (bool)true;
                    continue;
                }
                result[part.Substring(0, pos).ToLower()] = part.Substring(pos + 1);
            }

            
            foreach(var key in Keys)
            {
               
                if (result[key]==null)continue;
                raw = result[key];
                parts = raw.Split('-');
               
                if ((parts.Length != 2) && (parts.Length != 1))
                {
                    FATAL("Invalid transport line: {0}", (raw));
                    return false;
                }
                string all = "";
                ushort data = 0;
                ushort rtcp = 0;
                if (parts.Length == 2)
                {
                    data = Convert.ToUInt16((parts[0]));
                    rtcp = Convert.ToUInt16((parts[1]));
                    if (((data % 2) != 0) || ((data + 1) != rtcp))
                    {
                        FATAL("Invalid transport line: {0}", (raw));
                        return false;
                    }
                    all =  data+"-"+rtcp;
                }
                else
                {
                    data = Convert.ToUInt16((parts[0]));
                    all = Convert.ToString(data);
                    rtcp = 0;
                }
                if (all != raw)
                {
                    FATAL("Invalid transport line: {0}", (raw));
                    return false;
                }
                result[key].SetValue();
                result[key]["data"] =data;
                result[key]["rtcp"] = rtcp;
                result[key]["all"] = all;
            }

            return true;
        }

        private static bool ParseSection(Variant result, string[] lines, uint start, long length)
        {
            for (var i = 0; ((i + start) < lines.Length) && (i < length); i++)
            {
                if (lines[i + start] == "")
                    continue;
                if (!ParseSDPLine(result, lines[i + start]))
                {
                    FATAL("Parsing line {0} failed", (lines[i + start]));
                    return false;
                }
            }
            return true;
        }

        private static bool ParseSDPLine(Variant result, string line)
        {
            //1. test if this is a valid line
            if (line.Length < 2)
            {
                FATAL("Invalid line: {0}", (line));
                return false;
            }
            if (!((line[0] >= 'a') && (line[0] <= 'z')))
            {
                FATAL("Invalid line: {0}", (line));
                return false;
            }
            if (line[1] != '=')
            {
                FATAL("Invalid line: {0}", (line));
                return false;
            }
            Variant node;
            switch (line[0])
            {
                case 'a':
                    string name;
                    Variant value = Variant.Get();
                    if (!ParseSDPLineA(out name, value, line.Substring(2)))
                        return false;
                    if (result[SDP_A] == VariantType.Null)
                        result[SDP_A].IsArray = false;
                    if (result[SDP_A,name] != null)
                    {
                        WARN("This attribute overrided a previous one");
                    }
                    result[SDP_A,name] = value;
                    return true;
                case 'b':
                    if (result[SDP_B] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineB(result[SDP_B] = Variant.Get(), line.Substring(2));
                case 'c':
                    if (result[SDP_C] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineC(result[SDP_C] = Variant.Get(), line.Substring(2));
                case 'e':
                    node = Variant.Get();
                    if (!ParseSDPLineE(node, line.Substring(2)))
                        return false;
                    if(result[SDP_E]==null)
                        result[SDP_E] = Variant.Get();
                    result[SDP_E].Add(node);
                    return true;
                case 'i':
                    
                        if (result[SDP_I] != null)
                        {
                            FATAL("Duplicate value: {0}", line);
                            return false;
                        }
                        return ParseSDPLineI(result[SDP_I] = Variant.Get(), line.Substring(2));
                    
                case 'k':
                    
                        if (result[SDP_K] != null)
                        {
                            FATAL("Duplicate value: {0}", line);
                            return false;
                        }
                        return ParseSDPLineK(result[SDP_K] = Variant.Get(), line.Substring(2));
                    
                case 'm':
                    
                        if (result[SDP_M] != null)
                        {
                            FATAL("Duplicate value: {0}", line);
                            return false;
                        }
                        return ParseSDPLineM(result[SDP_M] = Variant.Get(), line.Substring(2));
                    
                case 'o':
                    
                        if (result[SDP_O] != null)
                        {
                            FATAL("Duplicate value: {0}", line);
                            return false;
                        }
                        return ParseSDPLineO(result[SDP_O] = Variant.Get(), line.Substring(2));
                case 'p':
                    node = Variant.Get();
                    if (!ParseSDPLineP(node, line.Substring(2)))
                        return false;
                    if (result[SDP_P] == null)
                        result[SDP_P] = Variant.Get();
                    result[SDP_P].Add(node);
                    return true;
                case 'r':

                    if (result[SDP_R] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineR(result[SDP_R] = Variant.Get(), line.Substring(2));

                case 's':

                    if (result[SDP_S] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineS(result[SDP_S] = Variant.Get(), line.Substring(2));

                case 't':

                    if (result[SDP_T] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineT(result[SDP_T] = Variant.Get(), line.Substring(2));

                case 'u':

                    if (result[SDP_U] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineU(result[SDP_U] = Variant.Get(), line.Substring(2));
                case 'v':

                    if (result[SDP_V] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineV(result[SDP_V] = Variant.Get(), line.Substring(2));
                case 'z':

                    if (result[SDP_Z] != null)
                    {
                        FATAL("Duplicate value: {0}", line);
                        return false;
                    }
                    return ParseSDPLineZ(result[SDP_Z] = Variant.Get(), line.Substring(2));
                default:
                    FATAL("Invalid value: {0}", line);
                    return false;
            }
        }

        private static bool ParseSDPLineZ(Variant variant, string substring)
        {
            //not impltement
            return false;
        }

        private static bool ParseSDPLineV(Variant result, string line)
        {
            if (line != "0")
                return false;
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineU(Variant result, string line)
        {
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineT(Variant result, string line)
        {
            var parts = line.Split(' ');
            if (parts.Length != 2) return false;
            result["startTime"] = parts[0];
            result["stopTime"] = parts[1];
            return true;
        }

        private static bool ParseSDPLineS(Variant result, string line)
        {
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineR(Variant result, string line)
        {
            //not impltement
            return false;
        }

        private static bool ParseSDPLineP(Variant result, string line)
        {
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineO(Variant result, string line)
        {
            var parts = line.Split(' ');
            if (parts.Length != 6) return false;
            result["username"] = parts[0];
            result["sessionId"] = parts[1];
            result["version"] = parts[2];
            result["networkType"] = parts[3];
            result["addressType"] = parts[4];
            result["address"] = parts[5];
            if ((string)result["networkType"] != "IN")
            {
                FATAL("Unsupported network type: {0}", (result["networkType"]));
                return false;
            }

            if ((string)result["addressType"] != "IP4")
            {
                FATAL("Unsupported address type: {0}", (result["addressType"]));
                return false;
            }

            string ip = Dns.GetHostAddresses(result["address"]).ToString();
            if (ip == "")
            {
                WARN("Invalid address: {0}", (result["address"]));
            }
            result["ip_address"] = ip;

            return true;
        }

        private static bool ParseSDPLineM(Variant result, string line)
        {
            var parts = line.Split(' ');
            if (parts.Length != 4) return false;
            result["MEDIA_TYPE"] = parts[0];
            result["ports"] = parts[1];
            result["transport"] = parts[2];
            result["payloadType"] = parts[3];
            return true;
        }

        private static bool ParseSDPLineK(Variant result, string line)
        {
            //not impltement
            return false;
        }

        private static bool ParseSDPLineI(Variant result, string line)
        {
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineE(Variant result, string line)
        {
            result.SetValue(line);
            return true;
        }

        private static bool ParseSDPLineC(Variant result, string line)
        {
            var parts = line.Split(' ');
            if (parts.Length != 3) return false;
            result["networkType"] = parts[0];
            result["addressType"] = parts[1];
            result["connectionAddress"] = parts[2];
            return true;
        }

        private static bool ParseSDPLineB(Variant result, string line)
        {
            var parts = line.Split(':');
            if (parts.Length != 2) return false;
            result["modifier"] = parts[0];
            result["value"] = parts[1];
            if (parts[0] == "AS")
            {
                result.SetValue(Convert.ToUInt32(parts[1]));
            }
            else
            {
                WARN("Bandwidth modifier {0} not implemented",result["modifier"]);
                result.SetValue(0);
            }
            return true;
        }

        private static bool ParseSDPLineA(out string attributeName, Variant value, string line)
        {
            var pos = line.IndexOf(':');
            if (pos == -1 || pos == 0 || pos == line.Length - 1)
            {
                attributeName = line;
                value.SetValue(true);
                return true;
            }
            attributeName = line.Substring(0, pos);
            string rawValue = line.Substring(pos + 1);
            switch (attributeName)
            {
                case "control":
                    value.SetValue(rawValue);
                    return true;
                case "maxprate":
                    value.SetValue(Convert.ToDouble(rawValue.TrimStart()));
                    return true;
                case "rtpmap":
                {
                    var parts = rawValue.Split(' ');
                    if (parts.Length != 2) return false;
                    value["payloadType"] = Convert.ToByte(parts[0]);
                    parts = parts[1].Split('/');
                    if (parts.Length != 2 && parts.Length != 3) return false;
                    value["encodingName"] = parts[0];
                    if (string.Compare(value["encodingName"], "h264",StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                        value["encodingName"] = (byte)VideoCodec.H264;
                    }else if (
                        string.Compare(value["encodingName"], "mpeg4-generic", StringComparison.CurrentCultureIgnoreCase) ==
                        0)
                    {
                        value["encodingName"] = (byte)AudioCodec.Aac;
                    }else if (string.Compare(value["encodingName"], "speex", StringComparison.CurrentCultureIgnoreCase) == 0)
                    {
                            value["encodingName"] = (byte)AudioCodec.Speex;
                    }
                    else
                    {
                        WARN("Invalid codec: {0}", (value["encodingName"]));
                        value.SetValue();
                        return true;
                    }
                    value["clockRate"] = Convert.ToUInt32(parts[1]);
                    if (parts.Length == 3)
                    {
                        value["encodingParameters"] = parts[2];
                    }
                    return true;
                }
                case "fmtp":
                {
                    rawValue = rawValue.Replace("; ", ";");
                    var parts = rawValue.Split(' ');
                    if (parts.Length != 2) return false;
                    value["payloadType"] = Convert.ToByte(parts[0]);
                    parts = parts[1].Split(';');
                    foreach (var p in parts.Select(part => part.Split(new [] { '=' } ,2)))
                        value[p[0]] = p[1];
                    return true;
                }
                default:
                    if (!attributeName.StartsWith("x-"))
                    {
                        WARN("Attribute {0} whith value {1} note parsed", attributeName, rawValue);
                    }
                    value.SetValue(rawValue);
                        return true;
            }
        }
        private static Variant GetTrack(this Variant _this,uint index, string type)
        {
            ushort videoTracksCount = 0;
            ushort audioTracksCount = 0;
            ushort globalTrackIndex = 0;
            Variant result = Variant.Get();
            foreach (var value in _this[SDP_MEDIATRACKS].Children.Values)
            {
                if (value[SDP_M,"MEDIA_TYPE"] == type)
                {
                    if (type == "video")
                    {
                        videoTracksCount++;
                        if (videoTracksCount == (index + 1))
                        {
                            result = ParseVideoTrack(value);
                            break;
                        }
                    }
                    else if (type == "audio")
                    {
                        audioTracksCount++;
                        if (audioTracksCount == (index + 1))
                        {
                            //result = ParseAudioTrack(value);
                            result = value;
                            break;
                        }
                    }
                }
                globalTrackIndex++;
            }
            if (result != VariantType.Null)
            {
                result["globalTrackIndex"] = globalTrackIndex;
            }
            return result;
        }

        private static Variant ParseVideoTrack(Variant track)
        {
            Variant result = track;
            if (result[SDP_A]==null)
            {
                FATAL("Track with no attributes");
                return Variant.Get();
            }
            if (result[SDP_A,"control"] == null)
            {
                FATAL("Track with no control uri");
                return Variant.Get();
            }
            if (result[SDP_A,"rtpmap"] == null)
            {
                FATAL("Track with no rtpmap");
                return Variant.Get();
            }
            if (result[SDP_A,"fmtp"] == null)
            {
                FATAL("Track with no fmtp");
                return Variant.Get();
            }
            var fmtp = result[SDP_A,"fmtp"];

            if (fmtp["sprop-parameter-sets"] == null)
            {
                FATAL("Video doesn't have sprop-parameter-sets");
                return Variant.Get();
            }
            var temp = fmtp["sprop-parameter-sets"];
            var parts = ((string)temp).Split(',');
            if (parts.Length != 2)
            {
                FATAL("Video doesn't have sprop-parameter-sets");
                return Variant.Get();
            }
            temp.SetValue();
            temp["SPS"] = parts[0];
            temp["PPS"] = parts[1];

            return result;
        }
        private static Variant ParseAudioTrack(Variant track)
        {
            Variant result = track;
            if (result[SDP_A] == null)
            {
                FATAL("Track with no attributes");
                return Variant.Get();
            }
            if (result[SDP_A, "control"] == null)
            {
                FATAL("Track with no control uri");
                return Variant.Get();
            }
            if (result[SDP_A, "rtpmap"] == null)
            {
                FATAL("Track with no rtpmap");
                return Variant.Get();
            }
            if (result[SDP_A, "fmtp"] == null)
            {
                FATAL("Track with no fmtp");
                return Variant.Get();
            }
            var fmtp = result[SDP_A, "fmtp"];

            if (fmtp["config"] == null)
            {
                FATAL("Invalid fmtp line：{0}", fmtp);
                return Variant.Get();
            }
            if (fmtp["mode"] == null)
            {
                FATAL("Invalid fmtp line：{0}", fmtp);
                return Variant.Get();
            }
            if (fmtp["mode"].ToString().ToLower() != "aac-hbr")
            {
                FATAL("Invalid fmtp line:\n{0}", (fmtp.ToString()));
                return Variant.Get();
            }
            if (fmtp["SizeLength"] == null)
            {
                FATAL("Invalid fmtp line：{0}", fmtp);
                return Variant.Get();
            }
            if (fmtp["SizeLength"] != "13")
            {
                FATAL("Invalid fmtp line:\n{0}", (fmtp.ToString()));
                return Variant.Get();
            }
            if (fmtp["IndexLength"] != null)
            {
                if (fmtp["IndexLength"] != "3")
                {
                    FATAL("Invalid fmtp line:\n{0}", (fmtp.ToString()));
                    return Variant.Get();
                }
            }
            if (fmtp["IndexDeltaLength"] != null)
            {
                if (fmtp["IndexDeltaLength"] != "3")
                {
                    FATAL("Invalid fmtp line:\n{0}", (fmtp.ToString()));
                    return Variant.Get();
                }
            }
            return result;
        }
    }
}