namespace CSharpRTMP.Core.MediaFormats.mp4.boxes
{
    public class AtomMP4A: VersionedBoxAtom
    {
        private ushort _dataReferenceIndex;
        private ushort _innerVersion;
        private ushort _revisionLevel;
        private uint _vendor;
        private ushort _numberOfChannels;
        private ushort _sampleSizeInBits;
        private short _compressionId;
        private ushort _packetSize;
        private uint _sampleRate;
        private uint _samplesPerPacket;
        private uint _bytesPerPacket;
        private uint _bytesPerFrame;
        private uint _bytesPerSample;
        private AtomESDS _atomESDS;
        private AtomWAVE _atomWAVE;

        public AtomMP4A(MP4Document document, uint type, long size, long start) : base(document, type, size, start)
        {
        }

        public override void ReadData()
        {
            _dataReferenceIndex = Br.ReadUInt16();
            

            _innerVersion = Br.ReadUInt16();
           

            _revisionLevel = Br.ReadUInt16();
            

            _vendor = Br.ReadUInt32();
           
            _numberOfChannels = Br.ReadUInt16();
           

            _sampleSizeInBits = Br.ReadUInt16();
           

            _compressionId = Br.ReadInt16();
           
            _packetSize = Br.ReadUInt16();
            
            _sampleRate = Br.ReadUInt32();
            

            if (_innerVersion == 0)
            {
                return ;
            }


            _samplesPerPacket = Br.ReadUInt32();
            

            _bytesPerPacket = Br.ReadUInt32();
           
            _bytesPerFrame = Br.ReadUInt32();
            

            _bytesPerSample = Br.ReadUInt32();
           
        }

        public override void AtomCreated(BaseAtom atom)
        {
            switch (atom.Type)
            {
                case ESDS:
                    _atomESDS = (AtomESDS)atom;
                    break;
                case WAVE:
                    _atomWAVE = (AtomWAVE)atom;
                    break;
                case CHAN:
                   // _atomCHAN = (AtomCHAN)atom;
                    break;
            }
        }
    }
}