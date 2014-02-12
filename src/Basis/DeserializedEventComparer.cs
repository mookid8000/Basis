using System.Collections.Generic;

namespace Basis
{
    class DeserializedEventComparer : IComparer<DeserializedEvent>
    {
        public int Compare(DeserializedEvent x, DeserializedEvent y)
        {
            return x.SeqNo.CompareTo(y.SeqNo);
        }
    }
}