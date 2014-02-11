using System.Collections.Generic;

namespace Basis
{
    public static class EnumEx
    {
        public static IEnumerable<IEnumerable<TElement>> Partition<TElement>(this IEnumerable<TElement> elements, int partitionSize)
        {
            var list = new List<TElement>();

            foreach (var elem in elements)
            {
                list.Add(elem);

                if (list.Count != partitionSize) continue;

                yield return list;
                
                list = new List<TElement>();
            }

            if (list.Count > 0)
            {
                yield return list;
            }
        }
    }
}