using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using SpatialEnrichment;

namespace SpatialEnrichmentWrapper
{
    public interface ICoordinate
    {
        double EuclideanDistance(ICoordinate pivotCoord);
        double Norm();
        string ToString();
        string ToString(string format);
        double GetDimension(int dim);
        int GetDimensionality();
    }
}
