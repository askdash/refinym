using System.Collections.Generic;
using SubtypeInduction.TypeSystemRels;

namespace Tests
{
    public class MockVariationOfInformationColoredLattice : VariationOfInformationColoredLattice<NodeName>
    {
        public MockVariationOfInformationColoredLattice() : base(new SubtokenVariationOfInformationComputer(0)) { }

        public override List<HashSet<LatticeNode<NodeName>>> InferColoring(out double s, int maxNumIterations = 10000, int clusteringTimoutMinutes = 2 * 24 * 60) { s = 0; return null; }

        public void CacheInfo()
        {
            _viComputer.CacheGlobalInformation(AllNodes);
        }
    }

    public class MockColoredLattice : AbstractColoredLattice<NodeName>
    {
        public override List<HashSet<LatticeNode<NodeName>>> InferColoring(out double s, int maxNumIterations = 10000, int clusteringTimoutMinutes = 2 * 24 * 60) { s = 0; return null; }

    }

    public class SimpleNode
    {
        public string[] Name;
    }
}
