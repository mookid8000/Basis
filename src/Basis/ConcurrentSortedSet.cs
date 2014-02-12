using System.Collections.Generic;
using System.Linq;

namespace Basis
{
    class ConcurrentSortedSet<TItem>
    {
        readonly SortedSet<TItem> _sortedSet;
        public ConcurrentSortedSet(IComparer<TItem> comparer)
        {
            _sortedSet = new SortedSet<TItem>(comparer);
        }

        public void Add(TItem item)
        {
            lock (_sortedSet) _sortedSet.Add(item);
        }

        public TItem FirstOrDefault()
        {
            lock (_sortedSet) return _sortedSet.FirstOrDefault();
        }

        public void Remove(TItem item)
        {
            lock (_sortedSet) _sortedSet.Remove(item);
        }
    }
}