using System.Collections.Generic;
using System.Linq;

namespace Basis
{
    class ConcurrentSortedSet
    {
        readonly SortedSet<DeserializedEvent> _sortedSet;
        public ConcurrentSortedSet(IComparer<DeserializedEvent> comparer)
        {
            _sortedSet = new SortedSet<DeserializedEvent>(comparer);
        }

        public int Count
        {
            get { lock (_sortedSet) return _sortedSet.Count; }
        }

        public void Add(DeserializedEvent item)
        {
            lock (_sortedSet) _sortedSet.Add(item);
        }

        public DeserializedEvent FirstOrDefault()
        {
            lock (_sortedSet) return _sortedSet.FirstOrDefault();
        }

        public void Remove(DeserializedEvent item)
        {
            lock (_sortedSet) _sortedSet.Remove(item);
        }

        public long[] GetGaps(long lastSequenceNumber)
        {
            lock (_sortedSet)
            {
                if (!_sortedSet.Any())
                {
                    return new long[0];
                }

                var seqNosToReturn = new List<long>();
                var seqNosOfEvents = _sortedSet.Select(s => s.SeqNo).ToArray();

                for (var seqNo = lastSequenceNumber; seqNo < seqNosOfEvents.Max(); seqNo++)
                {
                    if (seqNosOfEvents.Contains(seqNo)) continue;

                    seqNosToReturn.Add(seqNo);
                }

                return seqNosToReturn.ToArray();
            }
        }
    }
}