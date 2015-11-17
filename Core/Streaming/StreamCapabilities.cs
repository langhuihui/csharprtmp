using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using CSharpRTMP.Common;
using CSharpRTMP.Core.MediaFormats;
using CSharpRTMP.Core.Streaming;

namespace CSharpRTMP.Core.Streaming
{
    public struct VideoAvc
    {
        public byte[] SPS;
        
        public byte[] PPS;
     
        public uint Rate;
        public Variant _SPSInfo;
        public Variant _PPSInfo;
        public uint Width;
        public uint Height;
        public uint _widthOverride;
        public uint _heightOverride;
        public ushort SpsLength => (ushort)(SPS?.Length ?? 0) ;
        public ushort PpsLength => (ushort)(PPS?.Length??0);

        public bool Init(byte[] pSPS, byte[] pPPS)
        {
            Clear();
            _SPSInfo = Variant.Get();
            _PPSInfo = Variant.Get();
            if (pSPS.Length == 0 || pSPS.Length > 65535 || pPPS.Length == 0 || pPPS.Length > 65535)
            {
                Logger.FATAL("Invalid SPS/PPS lengths");
                return false;
            }
            SPS = pSPS;
            PPS = pPPS;
            Rate = 90000;
            var ms = new MemoryStream(SpsLength-1);
            for (ushort i = 1; i < pSPS.Length; i++)
            {
                if (i + 2 < pSPS.Length - 1 && SPS[i] == 0 && SPS[i + 1] == 0 && SPS[i + 2] == 3)
                {
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    i += 2;
                }
                else
                {
                    ms.WriteByte(SPS[i]);
                }
            }
            ms.Position = 0;
            using (var spsReader = new BitReader(ms))
            {
                if (!ReadSPS(spsReader, _SPSInfo))
                {
                    Logger.WARN("Unable to parse SPS");
                }
                else
                {
                    _SPSInfo.Compact();
                    Width = ((uint)_SPSInfo["pic_width_in_mbs_minus1"] + 1) * 16;
                    Height = ((uint)_SPSInfo["pic_height_in_map_units_minus1"] + 1) * 16;
                    //		FINEST("_width: %u (%u); _height: %u (%u)",
                    //				_width, (uint32_t) _SPSInfo["pic_width_in_mbs_minus1"],
                    //				_height, (uint32_t) _SPSInfo["pic_height_in_map_units_minus1"]);
                }
            }
            ms = new MemoryStream(PpsLength-1);
            for (ushort i = 1; i < pPPS.Length; i++)
            {
                if (i + 2 < pPPS.Length - 1 && PPS[i] == 0 && PPS[i + 1] == 0 && PPS[i + 2] == 3)
                {
                    ms.WriteByte(0);
                    ms.WriteByte(0);
                    i += 2;
                }
                else
                {
                    ms.WriteByte(PPS[i]);
                }
            }
            ms.Position = 0;
            using (var ppsReader = new BitReader(ms))
            {
                if (!ReadPPS(ppsReader, _PPSInfo))
                {
                    Logger.WARN("Unable to read PPS info");
                }
            }
            return true;
        }

        private bool ReadPPS(BitReader ppsReader, Variant ppsInfo)
        {
            //7.3.2.2 Picture parameter set RBSP syntax
            //14496-10.pdf 44/280
            ppsInfo["pic_parameter_set_id"] = ppsReader.ReadExpGolomb("pic_parameter_set_id");
            ppsInfo["seq_parameter_set_id"] = ppsReader.ReadExpGolomb("seq_parameter_set_id");
            ppsInfo["entropy_coding_mode_flag"] = ppsReader.ReadBool();
            ppsInfo["pic_order_present_flag"] = ppsReader.ReadBool();
            ppsInfo["num_slice_groups_minus1"] = ppsReader.ReadExpGolomb("num_slice_groups_minus1");
            
            if (ppsInfo["num_slice_groups_minus1"] > 0)
            {
                long sliceGroupMapType = ppsInfo["slice_group_map_type"] = ppsReader.ReadExpGolomb("slice_group_map_type");

                switch (sliceGroupMapType)
                {
                    case 0:
                        ppsInfo["run_length_minus1"] = Variant.Get();
                        for (var i = 0; i < ppsInfo["num_slice_groups_minus1"]; i++)
                        {
                            var val = ppsReader.ReadExpGolomb("run_length_minus1");
                            ppsInfo["run_length_minus1"].Add(val);
                        }
                        break;
                    case 2:
                        ppsInfo["top_left"] = Variant.Get();
                        ppsInfo["bottom_right"] = Variant.Get();
                        for (var i = 0; i < ppsInfo["num_slice_groups_minus1"]; i++)
                        {
                            ppsInfo["top_left"].Add(ppsReader.ReadExpGolomb("top_left"));
                            ppsInfo["bottom_right"].Add(ppsReader.ReadExpGolomb("bottom_right"));
                        }
                        break;
                    case 3:
                    case 4:
                    case 5:
                        ppsInfo["slice_group_change_direction_flag"] = ppsReader.ReadBool();
                        ppsInfo["slice_group_change_rate_minus1"] = ppsReader.ReadExpGolomb("slice_group_change_rate_minus1");
                        break;
                    case 6:
                        ppsInfo["pic_size_in_map_units_minus1"] = ppsReader.ReadExpGolomb("pic_size_in_map_units_minus1");
                        ppsInfo["slice_group_id"] = Variant.Get();
                        for (var i = 0; i <= ppsInfo["pic_size_in_map_units_minus1"]; i++)
                        {
                            ppsInfo["slice_group_id"].Add(ppsReader.ReadExpGolomb("slice_group_id"));
                        }
                        break;
                }
            }
            ppsInfo["num_ref_idx_l0_active_minus1"] = ppsReader.ReadExpGolomb("num_ref_idx_l0_active_minus1");
            ppsInfo["num_ref_idx_l1_active_minus1"] = ppsReader.ReadExpGolomb("num_ref_idx_l1_active_minus1");
            ppsInfo["weighted_pred_flag"] = ppsReader.ReadBool();
            ppsInfo["weighted_bipred_idc"] = ppsReader.ReadBitsToInt(2);
            ppsInfo["pic_init_qp_minus26"] = ppsReader.ReadExpGolomb("pic_init_qp_minus26");
            ppsInfo["pic_init_qs_minus26"] = ppsReader.ReadExpGolomb("pic_init_qs_minus26");
            ppsInfo["chromqp_index_offset"] = ppsReader.ReadExpGolomb("chromqp_index_offset");
            ppsInfo["deblocking_filter_control_present_flag"] = ppsReader.ReadBool();
            ppsInfo["constrained_intrpred_flag"] = ppsReader.ReadBool();
            ppsInfo["redundant_pic_cnt_present_flag"] = ppsReader.ReadBool();
            return true;
        }

        private bool ReadSPS(BitReader spsReader, Variant spsInfo)
        {
            //7.3.2.1 Sequence parameter set RBSP syntax
            //14496-10.pdf 43/280
            spsInfo["profile_idc"] = spsReader.ReadBitsToByte();
            spsInfo["constraint_set0_flag"] = spsReader.ReadBool();
            spsInfo["constraint_set1_flag"] = spsReader.ReadBool();
            spsInfo["constraint_set2_flag"] = spsReader.ReadBool();
            spsInfo["reserved_zero_5bits"] = spsReader.ReadBitsToByte(5);
            spsInfo["level_idc"] = spsReader.ReadBitsToByte();
            spsInfo["seq_parameter_set_id"] = spsReader.ReadExpGolomb("seq_parameter_set_id");
            if (spsInfo["profile_idc"] >= 100)
            {
                spsInfo["chromformat_idc"] = spsReader.ReadExpGolomb("chromformat_idc");
                if (spsInfo["chromformat_idc"] == 3) spsInfo["residual_colour_transform_flag"] = spsReader.ReadBool();
                spsInfo["bit_depth_lumminus8"] = spsReader.ReadExpGolomb("bit_depth_lumminus8");
                spsInfo["bit_depth_chromminus8"] = spsReader.ReadExpGolomb("bit_depth_chromminus8");
                spsInfo["qpprime_y_zero_transform_bypass_flag"] = spsReader.ReadBool();
                spsInfo["seq_scaling_matrix_present_flag"] = spsReader.ReadBool();
                if (spsInfo["seq_scaling_matrix_present_flag"])
                {
                    for (byte i = 0; i < 8; i++)
                    {
                        if (spsReader.AvailableBits < 1)
                        {
                            Logger.FATAL("unable to read seq_scaling_list_present_flag not enought bits ");
                            return false;
                        }
                        if (spsReader.ReadBool())
                        {
                            if (i < 6)
                            {
                                if (scaling_list(spsReader, 16)) continue;
                                Logger.FATAL("scaling_list failed");
                                return false;
                            }
                            if (scaling_list(spsReader, 64)) continue;
                            Logger.FATAL("scaling_list failed");
                            return false;
                        }
                    }
                }
            }
            spsInfo["log2_max_frame_num_minus4"] = spsReader.ReadExpGolomb("log2_max_frame_num_minus4");
            spsInfo["pic_order_cnt_type"] = spsReader.ReadExpGolomb("pic_order_cnt_type");
            if ((ulong)spsInfo["pic_order_cnt_type"] == 0)
            {
                spsInfo["log2_max_pic_order_cnt_lsb_minus4"] = spsReader.ReadExpGolomb("log2_max_pic_order_cnt_lsb_minus4");
            }
            else if ((ulong)spsInfo["pic_order_cnt_type"] == 1)
            {
                spsInfo["deltpic_order_always_zero_flag"] = spsReader.ReadBool();
                spsInfo["offset_for_non_ref_pic"] = (long)spsReader.ReadExpGolomb("offset_for_non_ref_pic");
                spsInfo["offset_for_top_to_bottom_field"] = (long)spsReader.ReadExpGolomb("offset_for_top_to_bottom_field");
                spsInfo["num_ref_frames_in_pic_order_cnt_cycle"] = spsReader.ReadExpGolomb("num_ref_frames_in_pic_order_cnt_cycle");
                for (ulong i = 0; i < (ulong)spsInfo["num_ref_frames_in_pic_order_cnt_cycle"]; i++)
                {
                    var val = spsReader.ReadExpGolomb("offset_for_ref_frame value");
                    spsInfo["offset_for_ref_frame"].Add((long)val);
                }
            }
            spsInfo["num_ref_frames"] = spsReader.ReadExpGolomb("num_ref_frames");
            spsInfo["gaps_in_frame_num_value_allowed_flag"] = spsReader.ReadBool();
            spsInfo["pic_width_in_mbs_minus1"] = spsReader.ReadExpGolomb("pic_width_in_mbs_minus1");
            spsInfo["pic_height_in_map_units_minus1"] = spsReader.ReadExpGolomb("pic_height_in_map_units_minus1");
            spsInfo["frame_mbs_only_flag"] = spsReader.ReadBool();
            if (!spsInfo["frame_mbs_only_flag"])
                spsInfo["mb_adaptive_frame_field_flag"] = spsReader.ReadBool();
            spsInfo["direct_8x8_inference_flag"] = spsReader.ReadBool();
            spsInfo["frame_cropping_flag"] = spsReader.ReadBool();
            if (spsInfo["frame_cropping_flag"])
            {
                spsInfo["frame_crop_left_offset"] = spsReader.ReadExpGolomb("frame_crop_left_offset");
                spsInfo["frame_crop_right_offset"] = spsReader.ReadExpGolomb("frame_crop_right_offset");
                spsInfo["frame_crop_top_offset"] = spsReader.ReadExpGolomb("frame_crop_top_offset");
                spsInfo["frame_crop_bottom_offset"] = spsReader.ReadExpGolomb("frame_crop_bottom_offset");
            }
            spsInfo["vui_parameters_present_flag"] = spsReader.ReadBool();
            if (!spsInfo["vui_parameters_present_flag"]) return true;
           
            if (ReadSPSVUI(spsReader, spsInfo["vui_parameters"]= Variant.Get())) return true;
            Logger.FATAL("Unable to read VUI");
            return false;
        }

        private bool ReadSPSVUI(BitReader spsReader, Variant v)
        {
            //E.1.1 VUI parameters syntax
            //14496-10.pdf 267/280
            v["aspect_ratio_info_present_flag"] = spsReader.ReadBool();
            if (v["aspect_ratio_info_present_flag"])
            {
                v["aspect_ratio_idc"] = spsReader.ReadBitsToByte();
                if ((byte)v["aspect_ratio_idc"] == 255)
                {
                    v["sar_width"] = (ushort)spsReader.ReadBitsToShort();
                    v["sar_height"] = (ushort)spsReader.ReadBitsToShort();
                }
            }
            v["overscan_info_present_flag"] = spsReader.ReadBool();
            if (v["overscan_info_present_flag"])
                v["overscan_appropriate_flag"] = spsReader.ReadBool();
            v["video_signal_type_present_flag"] = spsReader.ReadBool();
            if (v["video_signal_type_present_flag"])
            {
                v["video_format"] = spsReader.ReadBitsToByte(3);
                v["video_full_range_flag"] = spsReader.ReadBool();
                v["colour_description_present_flag"] = spsReader.ReadBool();
                if (v["colour_description_present_flag"])
                {
                    v["colour_primaries"] = spsReader.ReadBitsToByte();
                    v["transfer_characteristics"] = spsReader.ReadBitsToByte();
                    v["matrix_coefficients"] = spsReader.ReadBitsToByte();
                }
            }
            v["chromloc_info_present_flag"] = spsReader.ReadBool();
            if (v["chromloc_info_present_flag"])
            {
                v["chromsample_loc_type_top_field"] = spsReader.ReadExpGolomb("chromsample_loc_type_top_field");
                v["chromsample_loc_type_bottom_field"] = spsReader.ReadExpGolomb("chromsample_loc_type_bottom_field");
            }
            v["timing_info_present_flag"] = spsReader.ReadBool();
            if (v["timing_info_present_flag"])
            {
                v["num_units_in_tick"] = spsReader.ReadBitsToInt();
                v["time_scale"] = spsReader.ReadBitsToInt();
                v["fixed_frame_rate_flag"] = spsReader.ReadBool();
            }
            v["nal_hrd_parameters_present_flag"] = spsReader.ReadBool();
            if (v["nal_hrd_parameters_present_flag"])
            {
                if (!ReadSPSVUIHRD(spsReader, v["nal_hrd"] = Variant.Get()))
                {
                    Logger.FATAL("Unable to read VUIHRD");
                    return false;
                }
            }
            v["vcl_hrd_parameters_present_flag"] = spsReader.ReadBool();
            if (v["vcl_hrd_parameters_present_flag"])
            {
                if (!ReadSPSVUIHRD(spsReader, v["vcl_hrd"] = Variant.Get()))
                {
                    Logger.FATAL("Unable to read VUIHRD");
                    return false;
                }
            }
            if (v["nal_hrd_parameters_present_flag"]
                    || v["vcl_hrd_parameters_present_flag"])
                v["low_delay_hrd_flag"] = spsReader.ReadBool();
            v["pic_struct_present_flag"] = spsReader.ReadBool();
            v["bitstream_restriction_flag"] = spsReader.ReadBool();
            if (v["bitstream_restriction_flag"])
            {
                v["motion_vectors_over_pic_boundaries_flag"] = spsReader.ReadBool();
                v["max_bytes_per_pic_denom"] = spsReader.ReadExpGolomb("max_bytes_per_pic_denom");
                v["max_bits_per_mb_denom"] = spsReader.ReadExpGolomb("max_bits_per_mb_denom");
                v["log2_max_mv_length_horizontal"] = spsReader.ReadExpGolomb("log2_max_mv_length_horizontal");
                v["log2_max_mv_length_vertical"] = spsReader.ReadExpGolomb("log2_max_mv_length_vertical");
                v["num_reorder_frames"] = spsReader.ReadExpGolomb("num_reorder_frames");
                v["max_dec_frame_buffering"] = spsReader.ReadExpGolomb("max_dec_frame_buffering");
            }
            return true;
        }

        private bool ReadSPSVUIHRD(BitReader spsReader, Variant v)
        {
            //E.1.2 HRD parameters syntax
            //14496-10.pdf 268/280
            v["cpb_cnt_minus1"] = spsReader.ReadExpGolomb("cpb_cnt_minus1");
            v["bit_rate_scale"] = spsReader.ReadBitsToByte(4);
            v["cpb_size_scale"] = spsReader.ReadBitsToByte(4);
            v["bit_rate_value_minus1"] = Variant.Get();
            v["cpb_size_value_minus1"] = Variant.Get();
            v["cbr_flag"] = Variant.Get();
            for (ulong i = 0; i <= v["cpb_cnt_minus1"]; i++)
            {
                var val = spsReader.ReadExpGolomb("bit_rate_value_minus1");
                v["bit_rate_value_minus1"].Add(val);
                val = spsReader.ReadExpGolomb("cpb_size_value_minus1");
                v["cpb_size_value_minus1"].Add(val);
                if (spsReader.AvailableBits < 1)
                {
                    Logger.FATAL("not enough data");
                    return false;
                }
                v["cbr_flag"].Add(spsReader.ReadBool());
            }
            v["initial_cpb_removal_delay_length_minus1"] = spsReader.ReadBitsToByte(5);
            v["cpb_removal_delay_length_minus1"] = spsReader.ReadBitsToByte(5);
            v["dpb_output_delay_length_minus1"] = spsReader.ReadBitsToByte(5);
            v["time_offset_length"] = spsReader.ReadBitsToByte(5);
            return true;
        }

        private static bool scaling_list(BitReader spsReader, byte sizeOfScalingList)
        {
            uint nextScale = 8;
            uint lastScale = 8;
            for (byte j = 0; j < sizeOfScalingList; j++)
            {
                if (nextScale != 0)
                {
                    var deltscale = spsReader.ReadExpGolomb();
                    nextScale = (uint)((lastScale + deltscale + 256) % 256);
                }
                lastScale = (nextScale == 0) ? lastScale : nextScale;
            }
            return true;
        }

        public void Clear()
        {
            if (SPS != null) SPS = null;
            if (PPS != null) PPS = null;
            Rate = 0;
        }

        public bool Serialize(H2NBinaryWriter writer)
        {
            writer.Write(SpsLength);
            writer.Write(SPS);
            writer.Write(PpsLength);
            writer.Write(PPS);
            writer.Write(_widthOverride);
            writer.Write(_heightOverride);
            return true;
        }

        public static bool Deserialize(Stream src, out VideoAvc dest)
        {
            dest = new VideoAvc();
            if (src.GetAvaliableByteCounts() < 2)
            {
                Logger.FATAL("Not enough data");
                return false;
            }
            var reader = new N2HBinaryReader(src);

            var _spsLength = reader.ReadUInt16();
            if (src.GetAvaliableByteCounts() < _spsLength + 2 + 8)
            {
                Logger.FATAL("Not enough data");
                return false;
            }
            var psps = reader.ReadBytes(_spsLength);
            var _ppsLength = reader.ReadUInt16();
            if (src.GetAvaliableByteCounts() < _ppsLength + 2 + 8)
            {
                Logger.FATAL("Not enough data");
                return false;
            }
            var ppps = reader.ReadBytes(_ppsLength);

            dest.Init(psps, ppps);
            dest._widthOverride = reader.ReadUInt32();
            dest._heightOverride = reader.ReadUInt32();

            return true;
        }

    }
    public struct AudioAac
    {
        public byte[] _pAAC;
        public uint _aacLength;
        byte _audioObjectType;
        byte _sampleRateIndex;
        public uint _sampleRate;
        byte _channelConfigurationIndex;
        public bool Init(Stream pBuffer, int length)
        {
            Clear();
            var oldPosition = pBuffer.Position;
            if (length < 2)
            {
                Logger.FATAL("Invalid length:{0}", length);
                return false;
            }
            var bitReader = new BitReader(pBuffer);

            _audioObjectType = bitReader.ReadBitsToByte(5);
            if ((_audioObjectType != 1)
                && (_audioObjectType != 2)
                && (_audioObjectType != 3)
                && (_audioObjectType != 4)
                && (_audioObjectType != 6)
                && (_audioObjectType != 17)
                && (_audioObjectType != 19)
                && (_audioObjectType != 20)
                && (_audioObjectType != 23)
                && (_audioObjectType != 39))
            {
                Logger.FATAL("Invalid _audioObjectType: {0}", _audioObjectType);
                return false;
            }
            //3. Read the sample rate index
            _sampleRateIndex = bitReader.ReadBitsToByte(4);
            if ((_sampleRateIndex == 13)
                    || (_sampleRateIndex == 14))
            {
                Logger.FATAL("Invalid sample rate: {0}", _sampleRateIndex);
                return false;
            }
            if (_sampleRateIndex == 15)
            {
                if (length < 5)
                {
                    Logger.FATAL("Invalid length:{0}", length);
                    return false;
                }
                _sampleRate = (uint)bitReader.ReadBitsToInt(24);
            }
            else
            {
                var rates = new uint[]{
			96000, 88200, 64000, 48000, 44100, 32000, 24000, 22050, 16000,
			12000, 11025, 8000, 7350
		};
                _sampleRate = rates[_sampleRateIndex];
            }

            //4. read the channel configuration index
            _channelConfigurationIndex = bitReader.ReadBitsToByte(4);
            if ((_channelConfigurationIndex == 0)
                    || (_channelConfigurationIndex >= 8))
            {
                Logger.FATAL("Invalid _channelConfigurationIndex: {0}", _channelConfigurationIndex);
                return false;
            }

            pBuffer.Position = oldPosition;
            _pAAC = new byte[length];
            pBuffer.Read(_pAAC, 0, length);
            _aacLength = (uint) length;
            return true;
        }

        public void Clear()
        {
            if (_pAAC != null) _pAAC = null;
            _aacLength = 0;
            _audioObjectType = 0;
            _sampleRateIndex = 0;
            _sampleRate = 0;
            _channelConfigurationIndex = 0;
        }

        public string GetRTSPFmtpConfig() => "config=" + string.Join("", _pAAC.Select(x => x.ToString("x2")));

        public bool Serialize(H2NBinaryWriter writer)
        {
            writer.Write(_aacLength);
            writer.Write(_pAAC);
            return true;
        }

        public static bool Deserialize(Stream src, out AudioAac dest)
        {
            dest = new AudioAac();
            var length = src.GetAvaliableByteCounts();
            if (length < 4)
            {
                Logger.FATAL("Not enough data");
                return false;
            }
            using (var reader = new N2HBinaryReader(src))
            {
                dest._aacLength = reader.ReadUInt32();
                if (length < 4 + dest._aacLength)
                {
                    Logger.FATAL("Not enough data");
                    return false;
                }
                if (!dest.Init(src, (int) dest._aacLength))
                {
                    Logger.FATAL("Unable to init AAC");
                    return false;
                }
            }
            return true;
        }
    }

}

public class StreamCapabilities
{
    public uint BandwidthHint;
    public VideoCodec VideoCodecId = VideoCodec.Unknown;
    public AudioCodec AudioCodecId = AudioCodec.Unknown;
    public uint Samplerate;
    public AudioSampleSize AudioSampleSize;
    public AudioSampleType AudioSampleType;
    public VideoFrameType VideoFrameType;
    public VideoAvc Avc;
    public AudioAac Aac;
    public static bool Deserialize(string seekFilePath, StreamCapabilities capabilities)
    {
        using (var file = MediaFile.Initialize(seekFilePath))
        {
            if (file == null)
            {
                Logger.FATAL("Unable to open seek file {0}", seekFilePath);
                return false;
            }

            var length = file.Br.ReadUInt32();
            if (length > 1024 * 1024)
            {
                Logger.FATAL("Invalid stream capabilities length in file {0}: {1}", seekFilePath, length);
                return false;
            }

            //var buffer = new MemoryStream();
            //buffer.ReadFromRepeat(0, (int)length);
            //if (!file.ReadBuffer(buffer, (int)length))
            //{
            //    Logger.FATAL("Unable to read stream capabilities payload from file {0}",seekFilePath);
            //    return false;
            //}

            //file.Close();

            if (!Deserialize(file.DataStream, capabilities))
            {
                Logger.FATAL("Unable to deserialize stream capabilities from file {0}", seekFilePath);
                return false;
            }
        }
        return true;
    }
    internal static bool Deserialize(Stream raw, StreamCapabilities _streamCapabilities)
    {
        var reader = new N2HBinaryReader(raw);
        //var length = raw.GetAvaliableByteCounts();
        //if (length < 28)
        //{
        //    Logger.FATAL("Not enough data");
        //    return false;
        //}
        var ver = reader.ReadUInt64();
        if (ver != Utils.__STREAM_CAPABILITIES_VERSION)
        {
            Logger.FATAL("Invalid stream capabilities version. Wanted: {0}; Got: {1}",
            Utils.__STREAM_CAPABILITIES_VERSION, ver);
            return false;
        }
        _streamCapabilities.Clear();
        _streamCapabilities.VideoCodecId = (VideoCodec)reader.ReadByte();
        _streamCapabilities.AudioCodecId = (AudioCodec)reader.ReadByte();
        _streamCapabilities.BandwidthHint = reader.ReadUInt32();

        if (_streamCapabilities.VideoCodecId == VideoCodec.H264 && !VideoAvc.Deserialize(raw, out _streamCapabilities.Avc))
        {
            Logger.FATAL("Unable to deserialize avc");
            return false;
        }
        if (_streamCapabilities.AudioCodecId == AudioCodec.Aac &&
            !AudioAac.Deserialize(raw, out _streamCapabilities.Aac))
        {
            Logger.FATAL("Unable to deserialize aac");
            return false;
        }
        return true;
    }

    public void Clear()
    {
        ClearVideo();
        ClearAudio();
        BandwidthHint = 0;
    }

    private void ClearAudio()
    {
        if (AudioCodecId == AudioCodec.Aac)
        {
            Aac.Clear();
        }
        AudioCodecId = AudioCodec.PassThrough;
    }

    private void ClearVideo()
    {
        if (VideoCodecId == VideoCodec.H264)
            Avc.Clear();
        VideoCodecId = VideoCodec.PassThrough;
    }

    public bool Serialize(H2NBinaryWriter writer)
    {
        writer.Write(Utils.MakeTag("VER3"));
        writer.Write((byte)VideoCodecId);
        writer.Write((byte)AudioCodecId);
        writer.Write(BandwidthHint);

        if (VideoCodecId == VideoCodec.H264 && !Avc.Serialize(writer))
        {
            Logger.FATAL("Unable to serialize avc");
            return false;
        }
        if (AudioCodecId == AudioCodec.Aac && !Aac.Serialize(writer))
        {
            Logger.FATAL("Unable to serialize avc");
            return false;
        }
        return true;
    }

    internal bool InitVideoH264(byte[] pSPS, byte[] pPPS)
    {
        ClearVideo();
        if (!Avc.Init(pSPS, pPPS))
        {
            ClearVideo();
            return false;
        }
        VideoCodecId = VideoCodec.H264;
        return true;
    }

    public bool InitAudioAAC(Stream pBuffer, int length)
    {
        ClearAudio();
        if (!Aac.Init(pBuffer, length))
        {
            ClearAudio();
            return false;
        }
        AudioCodecId = AudioCodec.Aac;
        return true;
    }
}
