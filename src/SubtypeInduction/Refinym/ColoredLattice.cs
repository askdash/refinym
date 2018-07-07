using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubtypeInduction.TypeSystemRels
{
    public abstract class AbstractColoredLattice<TData>
    {
        protected readonly HashSet<LatticeNode<TData>> _allNodes = new HashSet<LatticeNode<TData>>();

        public IEnumerable<LatticeNode<TData>> AllNodes => _allNodes;

        // Do the actual inference of the lattice coloring.
        public abstract List<HashSet<LatticeNode<TData>>> InferColoring(out double score,
            int maxNumIterations = 2500, int clusteringTimoutMinutes = 2 * 24 * 60);

        public int NumRelationships { get; private set; }

        public Dictionary<T, LatticeNode<TData>> Add<T>(Dictionary<T, HashSet<T>> parentChildrenRelations, Func<T, TData> nodeData) where T : class
        {

            var externalToInternalNode = parentChildrenRelations.Keys.
                Concat(parentChildrenRelations.Values.SelectMany(s => s)).Distinct().
                ToDictionary(n => n, n => new LatticeNode<TData>(nodeData(n)));
            _allNodes.UnionWith(externalToInternalNode.Values);

            /*externalToInternalNode = parentChildrenRelations.Keys.
                Concat(parentChildrenRelations.Values.SelectMany(s => s)).GroupBy(s => s.GetHashCode()).Select(s => s.First()).
                ToDictionary(n => n, n => new LatticeNode<TData>(nodeData(n)));
            _allNodes.UnionWith(externalToInternalNode.Values);*/

            foreach (var relationship in parentChildrenRelations)
            {
                /*var parentNode = externalToInternalNode[relationship.Key];*/
                var parentNode = externalToInternalNode.Where(
                    kv => kv.Key.GetHashCode() == relationship.Key.GetHashCode()).Select(kv => kv.Value).First();

                foreach (var child in relationship.Value.Select(c => externalToInternalNode.Where(
                    kv => kv.Key.GetHashCode() == c.GetHashCode()).Select(kv => kv.Value).First()))
                {
                    parentNode.Children.Add(child);
                    child.Parents.Add(parentNode);
                    NumRelationships++;
                }
                /*
                foreach (var child in relationship.Value.Select(c => externalToInternalNode[c]))
                {
                    parentNode.Children.Add(child);
                    child.Parents.Add(parentNode);
                    NumRelationships++;
                }*/

            }
            return externalToInternalNode;
        }

        /// <summary>
        /// Find all nodes (including this one) that are connected to this node.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static HashSet<LatticeNode<TData>> TransitiveClosure(LatticeNode<TData> node)
        {
            var closure = new HashSet<LatticeNode<TData>>();

            var toVisit = new Stack<LatticeNode<TData>>();
            toVisit.Push(node);

            while (toVisit.Count > 0)
            {
                var nextNode = toVisit.Pop();
                if (closure.Contains(nextNode)) continue;
                closure.Add(nextNode);
                foreach (var otherNode in nextNode.Parents.Concat(nextNode.Children).Where(n => !closure.Contains(n)))
                {
                    toVisit.Push(otherNode);
                }
            }
            return closure;
        }

        /// <summary>
        /// Return all type root nodes in the closure of types implemented by typeDirectParents.
        /// </summary>
        /// <returns></returns>
        public static HashSet<LatticeNode<TData>> TransitiveClosureTypeParents(List<LatticeNode<TData>> typeDirectParents, HashSet<LatticeNode<TData>> typeRoots)
        {
            var closure = new HashSet<LatticeNode<TData>>();

            var parentsToVisit = new Stack<LatticeNode<TData>>();
            foreach (var parentNode in typeDirectParents)
            {
                parentsToVisit.Push(parentNode);
            }
            while (parentsToVisit.Count > 0)
            {
                var currentNode = parentsToVisit.Pop();
                if (typeRoots.Contains(currentNode)) closure.Add(currentNode);
                foreach (var parentNode in currentNode.Parents.Where(p => !closure.Contains(p)))
                {
                    parentsToVisit.Push(parentNode);
                }
            }
            return closure;
        }

        /// <summary>
        /// Return the transitive closure of children within a type.
        /// </summary>
        /// <param name="nodesInType"></param>
        /// <param name="newTypeRoot"></param>
        /// <returns></returns>
        public static HashSet<LatticeNode<TData>> GetTransitiveChildrenClosure(HashSet<LatticeNode<TData>> nodesInType, LatticeNode<TData> newTypeRoot)
        {
            var transitiveClosuresForType = new HashSet<LatticeNode<TData>>();
            var toVisit = new Stack<LatticeNode<TData>>();
            toVisit.Push(newTypeRoot);
            while (toVisit.Count > 0)
            {
                var nextNode = toVisit.Pop();
                transitiveClosuresForType.Add(nextNode);
                foreach (var child in nextNode.Children.Except(transitiveClosuresForType).Where(c => nodesInType.Contains(c)))
                {
                    toVisit.Push(child);
                }
            }
            return transitiveClosuresForType;
        }

        /// <summary>
        /// Return the transitive closure of parent within a type.
        /// </summary>
        /// <param name="nodesInType"></param>
        /// <param name="newTypeRoot"></param>
        /// <returns></returns>
        public static HashSet<LatticeNode<TData>> GetTransitiveParentClosure(HashSet<LatticeNode<TData>> nodesInType)
        {
            var transitiveClosuresForType = new HashSet<LatticeNode<TData>>();
            var toVisit = new Stack<LatticeNode<TData>>();
            foreach (var n in nodesInType) toVisit.Push(n);
            while (toVisit.Count > 0)
            {
                var nextNode = toVisit.Pop();
                transitiveClosuresForType.Add(nextNode);
                foreach (var child in nextNode.Parents.Except(transitiveClosuresForType))
                {
                    toVisit.Push(child);
                }
            }
            return transitiveClosuresForType;
        }

        /// <summary>
        /// Return the cycle paths.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<List<LatticeNode<TData>>> DetectCycles()
        {
            var paths = new Stack<(HashSet<LatticeNode<TData>>, List<LatticeNode<TData>>)>();
            foreach (var node in _allNodes)
            {
                var path = new List<LatticeNode<TData>>() { node };
                paths.Push((new HashSet<LatticeNode<TData>>(), path));
            }

            while (paths.Count > 0)
            {
                (var visitedNodes, var currentPath) = paths.Pop();

                foreach (var parent in currentPath[currentPath.Count - 1].Parents.Where(p => !visitedNodes.Contains(p)))
                {
                    var newPath = new List<LatticeNode<TData>>(currentPath)
                    {
                        parent
                    };

                    if (parent.Equals(currentPath[0]))
                    {
                        yield return newPath;
                    }
                    else
                    {
                        var newVisitedNodes = new HashSet<LatticeNode<TData>>(visitedNodes) { parent };
                        paths.Push((newVisitedNodes, newPath));
                    }
                }
            }
        }

        public bool ClustersAreCyclic(List<HashSet<LatticeNode<TData>>> clusters)
        {
            var parents = new List<HashSet<int>>();
            foreach (var cluster in clusters)
            {
                var nodesInColor = new HashSet<LatticeNode<TData>>(cluster);
                var parentNodes = new HashSet<LatticeNode<TData>>(cluster.SelectMany(n => n.Parents).Where(n => !nodesInColor.Contains(n)));
                var parentColorIds = new HashSet<int>();
                foreach (var parentNode in parentNodes)
                {
                    for (int i = 0; i < clusters.Count; i++)
                    {
                        if (clusters[i].Contains(parentNode))
                        {
                            parentColorIds.Add(i);
                            break;
                        }
                    }
                }
                parents.Add(parentColorIds);
            }

            var paths = new Stack<(HashSet<int> VisitedNodes, List<int> Path)>();
            for (int i = 0; i < clusters.Count; i++)
            {
                var path = new List<int>() { i };
                paths.Push((new HashSet<int>(), path));
            }

            while (paths.Count > 0)
            {
                (var visitedClusters, var currentPath) = paths.Pop();

                foreach (var parent in parents[currentPath[currentPath.Count - 1]])
                {
                    var newPath = new List<int>(currentPath)
                    {
                        parent
                    };

                    if (parent.Equals(currentPath[0]))
                    {
                        return true;
                    }
                    else
                    {
                        var newVisitedClusters = new HashSet<int>(visitedClusters) { parent };
                        paths.Push((newVisitedClusters, newPath));
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Return all anscestor clusters of the given cluster
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="nodeColoring"></param>
        /// <returns></returns>
        public static List<HashSet<LatticeNode<TData>>> GetClusterAncestors(HashSet<LatticeNode<TData>> cluster, List<HashSet<LatticeNode<TData>>> nodeColoring)
        {
            var parentClosure = GetTransitiveParentClosure(cluster);
            parentClosure.ExceptWith(cluster);
            return nodeColoring.Where(cl => cl.Intersect(parentClosure).Count() > 0).ToList();
        }

        // Checks if a coloring is valid.
        public void AssertColoringValid(List<HashSet<LatticeNode<TData>>> nodeColoring, List<HashSet<LatticeNode<TData>>> colorParents)
        {
            if (nodeColoring.Count != colorParents.Count)
            {
                throw new Exception("Num of types in first argument must be the same as those in the second");
            }
            if (!_allNodes.SetEquals(new HashSet<LatticeNode<TData>>(nodeColoring.SelectMany(n => n))))
            {
                throw new Exception("Coloring is missing some nodes");
            }

            var allTypeParentNodes = new HashSet<LatticeNode<TData>>(colorParents.SelectMany(n => n));

            var nodesWithNoParents = nodeColoring.SelectMany(n => n).Where(n => n.Parents.Count == 0).ToList();
            foreach (var node in nodesWithNoParents)
            {
                if (!allTypeParentNodes.Contains(node))
                {
                    throw new Exception("Node with no parents should also be a type parent");
                }
            }

            for (int i = 0; i < nodeColoring.Count; i++)
            {
                var colorNodes = new HashSet<LatticeNode<TData>>(nodeColoring[i]);
                foreach (var parent in colorParents[i])
                {
                    if (!colorNodes.Contains(parent)) throw new Exception("Color parent is not included within the color.");
                }
            }

            var frontier = new HashSet<LatticeNode<TData>>(nodesWithNoParents);
            var visited = new HashSet<LatticeNode<TData>>();
            var nodeToTypeParents = new Dictionary<LatticeNode<TData>, HashSet<LatticeNode<TData>>>();

            // Climb up the lattice in topo-order maintaining for each node the full set of "type parents" it implements.
            while (frontier.Count > 0)
            {
                var nextNode = frontier.First(n => n.Parents.All(p => visited.Contains(p)));
                visited.Add(nextNode);
                frontier.Remove(nextNode);
                var transitiveParents = new HashSet<LatticeNode<TData>>();
                if (allTypeParentNodes.Contains(nextNode))
                {
                    transitiveParents.Add(nextNode);
                }
                transitiveParents.UnionWith(nextNode.Parents.SelectMany(n => nodeToTypeParents[n]));
                nodeToTypeParents.Add(nextNode, transitiveParents);  // nextNode shouldn't exist in the dictionary
                frontier.UnionWith(nextNode.Children);
            }

            // Then check if for each type, all its nodes have identical type parents.
            for (int i = 0; i < nodeColoring.Count; i++)
            {
                var group = nodeColoring[i];
                var groupParents = colorParents[i];
                var parentsForAllNodes = group.Select(n => nodeToTypeParents[n]).ToArray();
                for (int j = 1; j < parentsForAllNodes.Length; j++)
                {
                    // Parents of the same group may be excluded
                    if (!new HashSet<LatticeNode<TData>>(parentsForAllNodes[j - 1].Except(groupParents)).SetEquals(
                         new HashSet<LatticeNode<TData>>(parentsForAllNodes[j]).Except(groupParents)))
                    {
                        throw new Exception("Nodes in the same type should have the same transitive closure of parents.");
                    }
                }

                foreach (var node in group)
                {
                    if (node.Parents.Any(p => !group.Contains(p)) || node.Parents.Count == 0)
                    {
                        // node must be a parent
                        if (!groupParents.Contains(node))
                        {
                            throw new Exception("Node has parents not in this color, but isn't a parent");
                        }
                    }
                }
            }
        }

        protected IEnumerable<SublatticeModification<TData>> ProposeSplitModification(List<HashSet<LatticeNode<TData>>> currentColoring, List<HashSet<LatticeNode<TData>>> currentColoringParents)
        {
            Debug.Assert(currentColoring.Count == currentColoringParents.Count);
            return Enumerable.Range(0, currentColoring.Count).AsParallel().
                SelectMany(i => SplitClusterOnNode.ProposeSplits(currentColoring[i], currentColoringParents[i]));
        }

        public static IEnumerable<LatticeNode<TData>> GetParentNodesForType(HashSet<LatticeNode<TData>> nodesInGroup)
        {
            return nodesInGroup.AsParallel().Where(n =>
            {
                if (n.Parents.Count == 0) return true;
                if (n.Parents.Any(p => !nodesInGroup.Contains(p))) return true;
                // This assumes that the sub-lattice is a true sublattice.
                return false;
            });
        }

        public List<HashSet<LatticeNode<TData>>> GetLatticeIdependentComponents()
        {
            var visited = new HashSet<LatticeNode<TData>>();
            var components = new List<HashSet<LatticeNode<TData>>>();

            foreach (var node in _allNodes)
            {
                if (visited.Contains(node))
                {
                    continue;
                }
                var closure = TransitiveClosure(node);
                visited.UnionWith(closure);
                components.Add(closure);
            }
            return components;
        }

        protected static void ApplyModification(List<HashSet<LatticeNode<TData>>> currentColoring, List<HashSet<LatticeNode<TData>>> currentColoringParents, SublatticeModification<TData> modification)
        {
            for (int i = 0; i < modification.After.Count; i++)
            {
                Debug.Assert(modification.AfterParents[i].Except(modification.After[i]).Count() == 0);
            }
            var removed = true;
            foreach (var beforeType in modification.Before)
            {
                removed &= currentColoring.Remove(beforeType);
            }
            foreach (var beforeParents in modification.BeforeParent)
            {
                removed &= currentColoringParents.Remove(beforeParents);
            }
            Debug.Assert(removed);

            currentColoring.AddRange(modification.After);
            currentColoringParents.AddRange(modification.AfterParents);
        }
    }

    [DebuggerDisplay("LatticeNode({Data})")]
    public class LatticeNode<TData>
    {
        public LatticeNode(TData data)
        {
            Data = data;
        }

        public TData Data { get; }
        public readonly List<LatticeNode<TData>> Parents = new List<LatticeNode<TData>>();
        public readonly List<LatticeNode<TData>> Children = new List<LatticeNode<TData>>();

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Object.ReferenceEquals(this, obj);
        }
    }
}
