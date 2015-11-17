using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpRTMP.Core.Protocols.Rtmfp
{
    public class Group : Entity,IEnumerable<Peer>,IEnumerable<KeyValuePair<uint,Peer>>
    {
        public Dictionary<uint, Peer> Peers = new Dictionary<uint, Peer>();

        public int Count => Peers.Count;

        IEnumerator<KeyValuePair<uint, Peer>> IEnumerable<KeyValuePair<uint, Peer>>.GetEnumerator()
        {
            return Peers.GetEnumerator();
        }

        public IEnumerator<Peer> GetEnumerator()
        {
            return Peers.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
