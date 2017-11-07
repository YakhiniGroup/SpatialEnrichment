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
    }
}
