using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpatialEnrichment.Helpers
{
    public class CoordNeighborArray
    {
        private int[] NeighborIds;
        public IEnumerable<int> Neighbors => NeighborIds.TakeWhile(v => v != -1);
        private const int len = 2;
        public CoordNeighborArray()
        {
            NeighborIds = new int[len];
            for (var i = 0; i < len; i++) NeighborIds[i] = -1;
        }

        public void AddNeighbor(int id)
        {
            for (var i = 0; i < len; i++)
                if (NeighborIds[i] == -1)
                {
                    NeighborIds[i] = id;
                    break;
                }
        }
    }
}
