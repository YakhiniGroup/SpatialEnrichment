namespace SpatialEnrichmentWrapper
{
    public interface ICoordinate
    {
        int? CoordId { get; set; }
        double EuclideanDistance(ICoordinate pivotCoord);
        double Norm();
        string ToString();
        string ToString(string format);
        double GetDimension(int dim);
        int GetDimensionality();
    }
}
