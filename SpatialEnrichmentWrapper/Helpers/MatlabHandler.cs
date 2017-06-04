using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SpatialEnrichment.Helpers
{
    public sealed class MatlabHandler : IDisposable
    {
        public static Type activationContext = Type.GetTypeFromProgID("matlab.application.single");
        private static WeakReference _matlab;
        public MLApp.MLApp Matlab
        {
            get
            {
                if (_matlab == null)
                {
                    _matlab = new WeakReference(Activator.CreateInstance(activationContext));
                    ((MLApp.MLApp)_matlab.Target).Visible = 0;
                    ((MLApp.MLApp)_matlab.Target).Execute(string.Format(@"cd {0}", Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location))); 
                }
                return (MLApp.MLApp) _matlab.Target;
            }
        }

        public void PlotTesselation(IDictionary<int, double[]> arraylst)
        {
            object result = null;
            Matlab.PutWorkspaceData("a", "base", arraylst.Select(t => t.Value[0]).ToArray());
            Matlab.PutWorkspaceData("b", "base", arraylst.Select(t => t.Value[1]).ToArray());
            Matlab.Feval("PlotTesselation", 0, out result, "a","b");
        }

        public void Plot2D(IDictionary<int, double[]> arraylst, bool withNames = false)
        {
            if (Matlab == null)
            {
                _matlab = new WeakReference(Activator.CreateInstance(activationContext));
                Matlab.Visible = 0;
            }
            Matlab.PutWorkspaceData("a", "base", arraylst.Select(t=>t.Value[0]).ToArray());
            Matlab.PutWorkspaceData("b", "base", arraylst.Select(t=>t.Value[1]).ToArray());
            Matlab.Execute("scatter(a,b); axis square equal;");
            if (!withNames) return;
            Matlab.PutWorkspaceData("g", "base", arraylst.OrderBy(t => t.Key).Select(t => t.Key).ToArray());
            Matlab.Execute("names=arrayfun(@num2str,g);");
            Matlab.Execute("text(a,b,names');");
        }

        public void Plot3D(IDictionary<long, double[]> arraylst, bool withNames = false)
        {
            Plot3D(arraylst.Values.ToList());
            if (!withNames) return;
            Matlab.PutWorkspaceData("g", "base", arraylst.OrderBy(t => t.Key).Select(t => t.Key).ToArray());
            Matlab.Execute("names=arrayfun(@num2str,g);");
            Matlab.Execute("text(a,b,c,names');");

        }

        public void Plot3D(List<double[]> arraylst)
        {
            Plot3D(arraylst.Select(t => t[0]).ToArray(), arraylst.Select(t => t[1]).ToArray(), arraylst.Select(t => t[2]).ToArray());
        }

        public void Plot3D(double[] a, double[] b, double[] c)
        {
            if (Matlab == null)
            {
                _matlab = new WeakReference(Activator.CreateInstance(activationContext));
                Matlab.Visible = 0;
            }
            Matlab.PutWorkspaceData("a", "base", a);
            Matlab.PutWorkspaceData("b", "base", b);
            Matlab.PutWorkspaceData("c", "base", c);
            Matlab.Execute("scatter3(a,b,c); axis square equal;");
        }

        public void PlotQuiver3D(IDictionary<long, double[]> arraylst1,
            IDictionary<long, double[]> arraylst2, bool withNames = false)
        {
            var arr1 = arraylst1.OrderBy(t => t.Key).Select(t => t.Value).ToList();
            var arr2 = arraylst2.OrderBy(t => t.Key).Select(t => t.Value).ToList();

            if (Matlab == null)
            {
                _matlab = new WeakReference(Activator.CreateInstance(activationContext));
                Matlab.Visible = 0;
            }

            Matlab.PutWorkspaceData("a", "base", arr1.Select(t => t[0]).ToArray());
            Matlab.PutWorkspaceData("b", "base", arr1.Select(t => t[1]).ToArray());
            Matlab.PutWorkspaceData("c", "base", arr1.Select(t => t[2]).ToArray());
            Matlab.PutWorkspaceData("d", "base", arr2.Select(t => t[0]).ToArray());
            Matlab.PutWorkspaceData("e", "base", arr2.Select(t => t[1]).ToArray());
            Matlab.PutWorkspaceData("f", "base", arr2.Select(t => t[2]).ToArray());

            Matlab.Execute("quiver3(a,b,c,d,e,f);  axis square equal;");
            if (!withNames) return;
            Matlab.PutWorkspaceData("g", "base", arraylst2.OrderBy(t => t.Key).Select(t => t.Key).ToArray());
            Matlab.Execute("names=arrayfun(@num2str,g);");
            Matlab.Execute("text(a,b,c,names');");
        }

        public void Dispose()
        {
            if (_matlab!=null && _matlab.IsAlive)
            {
                Marshal.FinalReleaseComObject(_matlab.Target);
            }
        }
    }
}
