using Unity.Collections;

namespace DeadCargo.Sectors.Field
{
    /// <summary>The order a run of values is sorted into.</summary>
    public interface IFieldOrder<T> where T : struct
    {
        /// <summary>Whether <paramref name="a"/> belongs before <paramref name="b"/>.</summary>
        bool Before(in T a, in T b);
    }

    /// <summary>
    /// Sorts a run of a native array in place: a heap sort, so it needs no scratch memory, never degrades, and runs
    /// in Burst jobs.
    /// </summary>
    public static class FieldSort
    {
        public static void Sort<T, TOrder>(NativeArray<T> items, int start, int count, TOrder order)
            where T : struct
            where TOrder : struct, IFieldOrder<T>
        {
            for (int root = count / 2 - 1; root >= 0; --root)
                SiftDown(items, start, root, count, order);

            for (int end = count - 1; end > 0; --end)
            {
                T last = items[start + end];
                items[start + end] = items[start];
                items[start] = last;
                SiftDown(items, start, 0, end, order);
            }
        }

        // Keeps the value that belongs last at the top of the heap.
        private static void SiftDown<T, TOrder>(NativeArray<T> items, int start, int root, int count, TOrder order)
            where T : struct
            where TOrder : struct, IFieldOrder<T>
        {
            T moving = items[start + root];
            while (true)
            {
                int child = root * 2 + 1;
                if (child >= count)
                    break;

                T later = items[start + child];
                if (child + 1 < count)
                {
                    T other = items[start + child + 1];
                    if (order.Before(later, other))
                    {
                        later = other;
                        ++child;
                    }
                }

                if (!order.Before(moving, later))
                    break;

                items[start + root] = later;
                root = child;
            }

            items[start + root] = moving;
        }
    }

    /// <summary>Largest rock first; rocks of one size by seed, so a cell sorts the same every time.</summary>
    public struct LargestRockFirst : IFieldOrder<FieldRock>
    {
        public bool Before(in FieldRock a, in FieldRock b)
        {
            return a.Radius > b.Radius || (a.Radius == b.Radius && a.Seed < b.Seed);
        }
    }
}
