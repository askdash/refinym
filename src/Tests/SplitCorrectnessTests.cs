using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using SubtypeInduction.TypeSystemRels;

namespace Tests
{
    [TestClass]
    public class SplitCorrectnessTests
    {
        [TestMethod]
        public void TestIncorrectSplitA()
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

            var singleGroupParents = new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2] };
            var typeGroup = new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2], nodeMap[n3] };

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n3]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n2]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n2]}
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n2]}
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n2]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]}
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n2]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]}
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]}
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]},
                        new HashSet<LatticeNode<NodeName>>() {}
                    }));
        }

        [TestMethod]
        public void TestIncorrectSplitB()
        {
            var n1 = new SimpleNode() { Name = new[] { "n1" } };
            var n2 = new SimpleNode() { Name = new[] { "n2" } };
            var n3 = new SimpleNode() { Name = new[] { "n3" } };
            var n4 = new SimpleNode() { Name = new[] { "n4" } };
            var n5 = new SimpleNode() { Name = new[] { "n5" } };
            var n6 = new SimpleNode() { Name = new[] { "n6" } };
            var n7 = new SimpleNode() { Name = new[] { "n7" } };
            var n8 = new SimpleNode() { Name = new[] { "n8" } };
            var n9 = new SimpleNode() { Name = new[] { "n9" } };
            var n10 = new SimpleNode() { Name = new[] { "n10" } };

            var rels = new Dictionary<SimpleNode, HashSet<SimpleNode>>()
            {
                { n10, new HashSet<SimpleNode>() { n4, n7, n9 } },
                { n9, new HashSet<SimpleNode>() { } },
                { n8, new HashSet<SimpleNode>() { } },
                { n7, new HashSet<SimpleNode>() { n5, n6 } },
                { n6, new HashSet<SimpleNode>() { n8, n9 } },
                { n5, new HashSet<SimpleNode>() { n2 } },
                { n4, new HashSet<SimpleNode>() { n2, n3 } },
                { n3, new HashSet<SimpleNode>() { n1 } },
                { n2, new HashSet<SimpleNode>() { n1 } },
                { n1, new HashSet<SimpleNode>() }
            };

            var viLattice = new MockColoredLattice();
            var nodeMap = viLattice.Add(rels, n => new NodeName(n.Name));

            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5], nodeMap[n6],
                                            nodeMap[n7], nodeMap[n8], nodeMap[n9], nodeMap[n10]}
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                    });

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5], nodeMap[n6],
                                            nodeMap[n7], nodeMap[n8], nodeMap[n9], nodeMap[n10]}
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1]},
                    }));

            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5], nodeMap[n6],
                                            nodeMap[n7], nodeMap[n8], nodeMap[n9], nodeMap[n10]}
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n7]},
                    }));


            // Split on n6
            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5], 
                                            nodeMap[n7], nodeMap[n10]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n6], nodeMap[n8], nodeMap[n9], }
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n9], nodeMap[n6]},
                    });

            // Inverted order
            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n6], nodeMap[n8], nodeMap[n9], },
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5],
                                            nodeMap[n7], nodeMap[n10]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n9], nodeMap[n6]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                    });

            // Split on 8
            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5], nodeMap[n6],
                                            nodeMap[n7], nodeMap[n9], nodeMap[n10]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n8], }
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n8]},
                    });

            // Add same node in two places
            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n6], nodeMap[n8], nodeMap[n9], },
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n1], nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5],
                                            nodeMap[n7], nodeMap[n9], nodeMap[n10]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n9], nodeMap[n6]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                    }));


            // Split into two different parts
            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n6], nodeMap[n8], nodeMap[n9],},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n2], nodeMap[n3],
                                            nodeMap[n4], nodeMap[n5],
                                            nodeMap[n7], nodeMap[n9], nodeMap[n10]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n9], nodeMap[n6], nodeMap[n1]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                    }));

            // Miss a node in splitting
            Assert.ThrowsException<Exception>(() =>
                viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n2], nodeMap[n3]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n4], nodeMap[n5],
                                              nodeMap[n6], nodeMap[n7], nodeMap[n8], nodeMap[n9], nodeMap[n10]},
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n2], nodeMap[n3]},
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                    }));

            // Correct split in four points
            viLattice.AssertColoringValid(new List<HashSet<LatticeNode<NodeName>>>() {
                    new HashSet<LatticeNode<NodeName>>() {nodeMap[n3], nodeMap[n4], nodeMap[n5],
                                            nodeMap[n7], nodeMap[n10]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n6], nodeMap[n9]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2]},
                    new HashSet<LatticeNode<NodeName>>() { nodeMap[n8], }
                    },
                    new List<HashSet<LatticeNode<NodeName>>>() {
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n10]},
                        new HashSet<LatticeNode<NodeName>>() { nodeMap[n6], nodeMap[n9] },
                        new HashSet<LatticeNode<NodeName>>() { nodeMap[n1], nodeMap[n2] },
                        new HashSet<LatticeNode<NodeName>>() {nodeMap[n8]},
                    });

        }
    }
}
