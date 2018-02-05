using SpatialEnrichmentWrapper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichment.Helpers
{
    public static class Generics
    {
        public static void SaveToCSV(IEnumerable<double[]> coords, string outfile, bool wait = false)
        {
            SaveToCSV(coords.Select(t => string.Join(",", t.Select(c => c.ToString("R")))), outfile, wait);
        }

        public static void SaveToCSV(IEnumerable<string> coords, string outfile, bool wait = false)
        {
            if (!Directory.Exists(Path.GetDirectoryName(Path.GetFullPath(outfile))))
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outfile)));
            var tsk = Task.Run(() =>
            {
                File.WriteAllText(outfile, string.Join("\n", coords.Select(c => c.ToString())));
            });
            if (wait)
                tsk.Wait();
        }

        public static void SaveToCSV(List<Coordinate> coords, string outfile, bool wait = false)
        {
            SaveToCSV(coords.Select(c => new[] { c.X, c.Y }).ToList(), outfile, wait);
        }

        public static void SaveToCSV(List<Coordinate3D> coords, string outfile, bool wait = false)
        {
            SaveToCSV(coords.Select(c => new[] { c.X, c.Y, c.Z }).ToList(), outfile, wait);
        }

        public static void SaveToCSV(Plane p, string outfile, bool wait = true)
        {
            SaveToCSV(new List<double[]>()
            {
                new[] { p.Normal.X, p.Normal.Y, p.Normal.Z },
                new[] { p.MidPoint.X, p.MidPoint.Y, p.MidPoint.Z }
            }, outfile, wait);
        }

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            var temp = lhs;
            lhs = rhs;
            rhs = temp;
        }
        public static Int32 BinarySearchIndexOf<T>(this IList<T> list, T value, IComparer<T> comparer = null)
        {
            if (list == null)
                throw new ArgumentNullException("list");

            comparer = comparer ?? Comparer<T>.Default;

            Int32 lower = 0;
            Int32 upper = list.Count - 1;

            while (lower <= upper)
            {
                Int32 middle = lower + (upper - lower) / 2;
                Int32 comparisonResult = comparer.Compare(value, list[middle]);
                if (comparisonResult == 0)
                    return middle;
                else if (comparisonResult < 0)
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            return -1;
        }


        public static IEnumerable<IEnumerable<T>> DifferentCombinations<T>(this IEnumerable<T> elements, int k)
        {
            var enumerable = elements as T[] ?? elements.ToArray();
            return k == 0 ? new[] { new T[0] } :
                enumerable.SelectMany((e, i) =>
                    enumerable.Skip(i + 1).DifferentCombinations(k - 1)
                        .Select(c => (new[] { e }).Concat(c)));
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            var list = new List<TSource>(count);
            foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
        }

        public static TSource[] ToArray<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (count < 0) throw new ArgumentOutOfRangeException("count");
            var array = new TSource[count];
            int i = 0;
            foreach (var item in source)
            {
                array[i++] = item;
            }
            return array;
        }

        public static TSource MaxBy<TSource, TProperty>(this IEnumerable<TSource> source,
            Func<TSource, TProperty> selector)
        {
            // check args        

            using (var iterator = source.GetEnumerator())
            {
                if (!iterator.MoveNext())
                    throw new InvalidOperationException();

                var max = iterator.Current;
                var maxValue = selector(max);
                var comparer = Comparer<TProperty>.Default;

                while (iterator.MoveNext())
                {
                    var current = iterator.Current;
                    var currentValue = selector(current);

                    if (comparer.Compare(currentValue, maxValue) > 0)
                    {
                        max = current;
                        maxValue = currentValue;
                    }
                }

                return max;
            }
        }
    }

    public class Normalizer
    {
        private int dim;
        private double[] botranges;
        private double[] topranges;
        private double[] denom => topranges.Zip(botranges, (a, b) => a - b).ToArray();
        public Normalizer(List<ICoordinate> coords)
        {
            dim = coords.First().GetDimensionality();
            botranges = new double[dim];
            topranges = new double[dim];
            for (var i = 0; i < dim; i++)
            {
                botranges[i] = coords.Min(c => c.GetDimension(i));
                topranges[i] = coords.Max(c => c.GetDimension(i));
            }
        }

        public IEnumerable<ICoordinate> Normalize(List<ICoordinate> coords)
        {
            switch (dim)
            {
                case 2:
                    foreach (var c in coords)
                        yield return new Coordinate((c.GetDimension(0) - botranges[0]) / denom[0], (c.GetDimension(1) - botranges[1]) / denom[1]);
                    break;
                case 3:
                    foreach (var c in coords)
                        yield return new Coordinate3D((c.GetDimension(0) - botranges[0]) / denom[0], (c.GetDimension(1) - botranges[1]) / denom[1], (c.GetDimension(2) - botranges[2]) / denom[2]);
                    break;
            }
        }

        public ICoordinate DeNormalize(ICoordinate c)
        {
            switch (dim)
            {
                case 2:
                    return new Coordinate(c.GetDimension(0) * denom[0] + botranges[0], c.GetDimension(1) * denom[1] + botranges[1]);
                case 3:
                    return new Coordinate3D(c.GetDimension(0) * denom[0] + botranges[0], c.GetDimension(1) * denom[1] + botranges[1], c.GetDimension(2) * denom[2] + botranges[2]);
            }
            return null;
        }

        public IEnumerable<ICoordinate> DeNormalize(List<ICoordinate> coords)
        {
            switch (dim)
            {
                case 2:
                    foreach (var c in coords)
                        yield return new Coordinate(c.GetDimension(0) * denom[0] + botranges[0], c.GetDimension(1) * denom[1] + botranges[1]);
                    break;
                case 3:
                    foreach (var c in coords)
                        yield return new Coordinate3D(c.GetDimension(0) * denom[0] + botranges[0], c.GetDimension(1) * denom[1] + botranges[1], c.GetDimension(2) * denom[2] + botranges[2]);
                    break;
            }
        }

    }


    public class SafeRandom
    {
        private static Random random;

        public SafeRandom()
        {
            random = new Random();
        }

        public SafeRandom(int seed)
        {
            random = new Random(seed);
        }

        public int Next()
        {
            lock (random)
            {
                return random.Next();
            }
        }

        public double NextDouble()
        {
            lock (random)
            {
                return random.NextDouble();
            }
        }
    }

}
