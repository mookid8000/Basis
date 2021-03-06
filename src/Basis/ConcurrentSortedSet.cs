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
            MaxSize = int.MaxValue;
        }

        public int Count
        {
            get
            {
                lock (_sortedSet)
                {
                    return _sortedSet.Count;
                }
            }
        }

        public int MaxSize { get; set; }
        public void Add(DeserializedEvent item)
        {
            lock (_sortedSet)
            {
                _sortedSet.Add(item);

                while (_sortedSet.Count > MaxSize)
                {
                    _sortedSet.Remove(_sortedSet.Max);
                }
            }
        }

        public DeserializedEvent FirstOrDefault()
        {
            lock (_sortedSet)
            {
                return _sortedSet.Count > 0
                    ? _sortedSet.Min
                    : null;
            }
        }

        public void Remove(DeserializedEvent item)
        {
            lock (_sortedSet)
            {
                _sortedSet.Remove(item);
            }
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

                for (var seqNo = lastSequenceNumber + 1; seqNo < seqNosOfEvents.Max(); seqNo++)
                {
                    if (seqNosOfEvents.Contains(seqNo)) continue;

                    seqNosToReturn.Add(seqNo);
                }

                return seqNosToReturn.ToArray();
            }
        }
    }
}