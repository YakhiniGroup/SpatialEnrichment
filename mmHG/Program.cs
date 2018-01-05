using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using mmHG.DataClasses;

namespace mmHG
{
    class Program
    {
        public static void Main(String[] args)
        {
            if (args.Length > 0)
            {
                FileAttributes attr = File.GetAttributes(args[0]);

                if (attr.HasFlag(FileAttributes.Directory))
                {
                    var bestFile = "";
                    var bestScore = double.MaxValue;
                    foreach (var file in Directory.EnumerateFiles(args[0], "*.txt"))
                    {
                        var res = ComputeScoreForFile(file);
                        File.WriteAllLines(Path.ChangeExtension(file, ".mmhg"), new[] { res.ToString() });
                        if (res.ScoreValue < bestScore)
                        {
                            bestFile = file;
                            bestScore = res.ScoreValue;
                        }
                    }
                    File.WriteAllLines("final.mmhg", new[] { $"Winner = {bestFile} Score = {bestScore}." });
                }
                else
                {
                    var res = ComputeScoreForFile(args[0]);
                    File.WriteAllLines(Path.ChangeExtension(args[0], ".mmhg"), new[] { res.ToString() });
                }
            }
            else
            {
                MHG.SetMaxN(1000);
                int[] perm = { 1, 4, 0, 3, 2 };
                Console.WriteLine(MMHG.CalcScore(perm));
                Console.ReadKey();
            }
        }

        private static MMHGScore ComputeScoreForFile(string args)
        {
            var perm = File.ReadAllLines(args).Select(v => int.Parse(v)).ToArray();
            if (perm.Min() == 1) //turn 0-based
                for (var i = 0; i < perm.Length; i++)
                    perm[i] -= 1;
            MHG.SetMaxN((int)(0.3 * perm.Max()));
            var res = MMHG.CalcScore(perm);
            return res;
        }
    }
}
