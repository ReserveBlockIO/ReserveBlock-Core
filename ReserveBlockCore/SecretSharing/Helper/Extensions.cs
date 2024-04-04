using System.Collections;

namespace ReserveBlockCore.SecretSharing.Helper
{
    internal static class Extensions
    {
        /// <summary>
        ///  Compares instance <paramref name="a"/> with instance <paramref name="b"/> and returns an indication of their relative values.
        /// </summary>
        /// <typeparam name="TStructural">A data type which implements <see cref="IStructuralComparable"/></typeparam>
        /// <param name="a">The current object to compare with the <paramref name="b"/> instance</param>
        /// <param name="b">The object to compare with the current instance</param>
        /// <returns>-1 if the <paramref name="a"/> instance (current) precedes <paramref name="b"/>, 0 the <paramref name="a"/> instance
        /// and <paramref name="b"/> instance are equal and 1 if the <paramref name="a"/> instance follows <paramref name="b"/>.</returns>
        public static int CompareTo<TStructural>(this TStructural a, TStructural b)
            where TStructural : IStructuralComparable => a.CompareTo(b, StructuralComparisons.StructuralComparer);

        /// <summary>
        /// Creates a new array (destination array) which is a subset of the original array (source array).
        /// </summary>
        /// <typeparam name="TArray">Data type of the array</typeparam>
        /// <param name="array">source array</param>
        /// <param name="index">start index</param>
        /// <param name="count">number of elements to copy to new subset array</param>
        /// <returns>An array that contains the specified number of elements from the <paramref name="index"/> of the <paramref name="array"/>.</returns>
        public static TArray[] Subset<TArray>(this TArray[] array, int index, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Length == 0)
            {
                throw new ArgumentException(nameof(array));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, string.Format("0"));
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, string.Format("1"));
            }

            var subset = new TArray[count];
            Array.Copy(array, index, subset, 0, count);
            return subset;
        }
    }
}
