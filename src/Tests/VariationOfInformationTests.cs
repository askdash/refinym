using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using SubtypeInduction.TypeSystemRels;

namespace Tests
{

    [TestClass]
    public class VariationOfInformationTests
    {        
        [TestMethod]
        public void TestVIIntuition()
        {
            var n1 = new SimpleNode() { Name = new[] { "n" } };
            var n2 = new SimpleNode() { Name = new[] { "n" } };
            var n3 = new SimpleNode() { Name = new[] { "n" } };

            var b1 = new SimpleNode() { Name = new[] { "b" } };
            var b2 = new SimpleNode() { Name = new[] { "b" } };
            var b3 = new SimpleNode() { Name = new[] { "b" } };

            var root = new SimpleNode() { Name = new[] { "root" } };

            var rels = new Dictionary<SimpleNode, HashSet<SimpleNode>>()
            {
                { n1, new HashSet<SimpleNode>() { n2, n3 } },
                { n2, new HashSet<SimpleNode>() { n3 } },
                { n3, new HashSet<SimpleNode>()  },

                { b1, new HashSet<SimpleNode>() { b2 } },
                { b2, new HashSet<SimpleNode>() { b3 } },
                { b3, new HashSet<SimpleNode>() },

                { root, new HashSet<SimpleNode>() { n1, b1 } },
            };

            var viLattice = new MockVariationOfInformationColoredLattice();
            var nodeMap = viLattice.Add(rels, n => new NodeName(n.Name));
            viLattice.CacheInfo();

            var grouping1 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3], nodeMap[b1], nodeMap[b2], nodeMap[b3], nodeMap[root] }
            };
            
            var viAllTogether = viLattice.ComputeVariationOfInformation(grouping1);

            var grouping2 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1], nodeMap[b2], nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplit = viLattice.ComputeVariationOfInformation(grouping2);
            Assert.IsTrue(viSplit < viAllTogether);

            var grouping3 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n2]},
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1], nodeMap[b2], nodeMap[b3]},
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplitTooMuch1 = viLattice.ComputeVariationOfInformation(grouping3);
            Assert.IsTrue(viSplit < viSplitTooMuch1);

            var grouping4 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n2]},
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b2] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplitTooMuch2 = viLattice.ComputeVariationOfInformation(grouping4);
            Assert.IsTrue(viSplit < viSplitTooMuch2);
            Assert.IsTrue(viSplitTooMuch1 < viSplitTooMuch2);
        }

        [TestMethod]
        public void TestVITwoTokens()
        {
            var n1 = new SimpleNode() { Name = new[] { "n", "Yellow" } };
            var n2 = new SimpleNode() { Name = new[] { "n", "Red" } };
            var n3 = new SimpleNode() { Name = new[] { "n", "Yellow" } };

            var b1 = new SimpleNode() { Name = new[] { "b", } };
            var b2 = new SimpleNode() { Name = new[] { "b", } };
            var b3 = new SimpleNode() { Name = new[] { "b", } };

            var root = new SimpleNode() { Name = new[] { "root" } };

            var rels = new Dictionary<SimpleNode, HashSet<SimpleNode>>()
            {
                { n1, new HashSet<SimpleNode>() { n2, n3 } },
                { n2, new HashSet<SimpleNode>() { n3 } },
                { n3, new HashSet<SimpleNode>()  },

                { b1, new HashSet<SimpleNode>() { b2 } },
                { b2, new HashSet<SimpleNode>() { b3 } },
                { b3, new HashSet<SimpleNode>() },

                { root, new HashSet<SimpleNode>() { n1, b1 } },
            };

            var viLattice = new MockVariationOfInformationColoredLattice();
            var nodeMap = viLattice.Add(rels, n => new NodeName(n.Name));

            viLattice.CacheInfo();

            var grouping1 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3], nodeMap[b1], nodeMap[b2], nodeMap[b3], nodeMap[root] }
            };
            
            var viAllTogether = viLattice.ComputeVariationOfInformation(grouping1);

            var grouping2 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1], nodeMap[b2], nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplit = viLattice.ComputeVariationOfInformation(grouping2);
            Assert.IsTrue(viSplit < viAllTogether);

            var groupingSpurious = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1], nodeMap[b2], nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };
            var viSpuriousSplit = viLattice.ComputeVariationOfInformation(groupingSpurious);
            Assert.IsTrue(viSplit < viSpuriousSplit);

            var grouping3 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[b1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n2], nodeMap[b2] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n3], nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplitWeird = viLattice.ComputeVariationOfInformation(grouping3);
            Assert.IsTrue(viSplit < viSplitWeird, "{0}<{1}", viSplit, viSplitWeird);

            var grouping4 = new List<HashSet<LatticeNode<NodeName>>>()
            {
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n2], },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[n3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b1] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b2] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[b3] },
                new HashSet<LatticeNode<NodeName>>() { nodeMap[root] }
            };

            var viSplitTooMuch2 = viLattice.ComputeVariationOfInformation(grouping4);
            Assert.IsTrue(viSplit < viSplitTooMuch2);
        }
    }
}
