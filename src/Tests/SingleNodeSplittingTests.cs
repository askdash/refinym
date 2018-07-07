using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using SubtypeInduction.TypeSystemRels;
using System.Linq;

namespace Tests
{
    [TestClass]
    public class SingleNodeSplittingTests
    {
        [TestMethod]
        public void TestSimpleChainSplit()
        {
            var n1 = new SimpleNode() { Name = new[] { "n1" } };
            var n2 = new SimpleNode() { Name = new[] { "n2" } };
            var n3 = new SimpleNode() { Name = new[] { "n3" } };

            var rels = new Dictionary<SimpleNode, HashSet<SimpleNode>>()
            {
                { n1, new HashSet<SimpleNode>() { n2 } },
                { n2, new HashSet<SimpleNode>() { n3 } },
                { n3, new HashSet<SimpleNode>() }
            };

            var viLattice = new MockColoredLattice();
            var nodeMap = viLattice.Add(rels, n => new NodeName(n.Name));

            var singleGroupParents = new HashSet<LatticeNode<NodeName>>() { nodeMap[n1] };
            var typeGroup = new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3] };

            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() { typeGroup }, 
                new List<HashSet<LatticeNode<NodeName>>>() { singleGroupParents });

            var split = SplitClusterOnNode.SplitOn(typeGroup, singleGroupParents, nodeMap[n2]);
            Assert.AreEqual(split.Before.Count, 1);
            Assert.AreEqual(split.BeforeParent.Count, 1);
            Assert.AreEqual(split.Before[0], typeGroup);
            Assert.AreEqual(split.BeforeParent[0], singleGroupParents);
            viLattice.AssertColoringValid(split.Before, split.BeforeParent);

            var parentsAsHashSet = new HashSet<LatticeNode<NodeName>>(split.AfterParents.SelectMany(n=>n));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[n1]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[n2]));

            Assert.IsTrue(split.After[0].Contains(nodeMap[n1]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n2]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n3]));

            viLattice.AssertColoringValid(split.After, split.AfterParents);
        }

        [TestMethod]
        public void TestSimpleVeeSplit()
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
            
            var singleGroupParents = new HashSet<LatticeNode<NodeName>>() { nodeMap[root] };
            var typeGroup = new HashSet<LatticeNode<NodeName>>() {
                nodeMap[n1], nodeMap[n2], nodeMap[n3],
                nodeMap[b1], nodeMap[b2], nodeMap[b3],
                nodeMap[root]
            };

            var split = SplitClusterOnNode.SplitOn(typeGroup, singleGroupParents, nodeMap[b1]);
            Assert.AreEqual(split.Before.Count, 1);
            Assert.AreEqual(split.BeforeParent.Count, 1);
            Assert.AreEqual(split.Before[0], typeGroup);
            Assert.AreEqual(split.BeforeParent[0], singleGroupParents);
            viLattice.AssertColoringValid(split.Before, split.BeforeParent);


            var parentsAsHashSet = new HashSet<LatticeNode<NodeName>>(split.AfterParents.SelectMany(n => n));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[root]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[b1]));

            Assert.IsTrue(split.After[0].Contains(nodeMap[root]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n1]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n2]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n3]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[b1]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[b2]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[b3]));
            viLattice.AssertColoringValid(split.After, split.AfterParents);

            split = SplitClusterOnNode.SplitOn(typeGroup, singleGroupParents, nodeMap[b2]);
            Assert.AreEqual(split.Before.Count, 1);
            Assert.AreEqual(split.BeforeParent.Count, 1);
            Assert.AreEqual(split.Before[0], typeGroup);
            Assert.AreEqual(split.BeforeParent[0], singleGroupParents);

            parentsAsHashSet = new HashSet<LatticeNode<NodeName>>(split.AfterParents.SelectMany(n => n));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[root]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[b2]));

            Assert.IsTrue(split.After[0].Contains(nodeMap[root]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n1]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n2]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n3]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b1]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[b2]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[b3]));
            viLattice.AssertColoringValid(split.After, split.AfterParents);

            split = SplitClusterOnNode.SplitOn(typeGroup, singleGroupParents, nodeMap[n1]);
            Assert.AreEqual(split.Before.Count, 1);
            Assert.AreEqual(split.BeforeParent.Count, 1);
            Assert.AreEqual(split.Before[0], typeGroup);
            Assert.AreEqual(split.BeforeParent[0], singleGroupParents);

            parentsAsHashSet = new HashSet<LatticeNode<NodeName>>(split.AfterParents.SelectMany(n => n));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[root]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[n1]));

            Assert.IsTrue(split.After[0].Contains(nodeMap[root]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b1]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b2]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b3]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n1]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n2]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n3]));

            viLattice.AssertColoringValid(split.After, split.AfterParents);
        }

        [TestMethod]
        public void TestForcedTwoVeeSplit()
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

            var singleGroupParents = new HashSet<LatticeNode<NodeName>>() { nodeMap[root] };
            var typeGroup = new HashSet<LatticeNode<NodeName>>() {
                nodeMap[n1], nodeMap[n2], nodeMap[n3],
                nodeMap[b1], nodeMap[b2], nodeMap[b3],
                nodeMap[root]
            };

            var split = SplitClusterOnNode.SplitOn(typeGroup, singleGroupParents, nodeMap[n2]);
            Assert.AreEqual(split.Before.Count, 1);
            Assert.AreEqual(split.BeforeParent.Count, 1);
            Assert.AreEqual(split.Before[0], typeGroup);
            Assert.AreEqual(split.BeforeParent[0], singleGroupParents);
            viLattice.AssertColoringValid(split.Before, split.BeforeParent);

            var parentsAsHashSet = new HashSet<LatticeNode<NodeName>>(split.AfterParents.SelectMany(n => n));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[root]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[n2]));
            Assert.IsTrue(parentsAsHashSet.Contains(nodeMap[n3]));

            Assert.IsTrue(split.After[0].Contains(nodeMap[root]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b1]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b2]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[b3]));
            Assert.IsTrue(split.After[0].Contains(nodeMap[n1]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n2]));
            Assert.IsTrue(split.After[1].Contains(nodeMap[n3]));
            viLattice.AssertColoringValid(split.After, split.AfterParents);
        }

    }
}
