using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SubtypeInduction.TypeSystemRels
{
    public enum ModificationType
    {
        None = 0,
        Merge,
        Split
    }

    /// <summary>
    /// Class to represent a modification to a part of the lattice (=sublattice)
    /// </summary>
    public class SublatticeModification<T>
    {
        public ModificationType Type;
        public object Data;

        // The list of nodes that exist in the type before a split
        public List<HashSet<LatticeNode<T>>> Before;
        // The list of parent nodes in the types. This may be more than one in some cases.
        public List<HashSet<LatticeNode<T>>> BeforeParent;

        // A list of lists of the nodes that exist in the splitted types
        public List<HashSet<LatticeNode<T>>> After;
        // A list of lists of the parents of each new splitted type
        public List<HashSet<LatticeNode<T>>> AfterParents;

        public override int GetHashCode()
        {
            return BeforeParent.Count.GetHashCode() ^ Type.GetHashCode() ^
                After.Count.GetHashCode() ^ Before.Count.GetHashCode();
        }

        private bool ListOfSetEquals(List<HashSet<LatticeNode<T>>> l1, List<HashSet<LatticeNode<T>>> l2)
        {
            Debug.Assert(l1.Count == l2.Count); // Precondition: lists are of equal length
            for (int i = 0; i < l1.Count; i++)
            {
                if (!l1[i].SetEquals(l2[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is SublatticeModification<T> other)
            {
                if (other.Type != Type) return false;
                if (Before.Count != other.Before.Count ||
                    BeforeParent.Count != other.BeforeParent.Count ||
                    After.Count != other.After.Count ||
                    AfterParents.Count != other.AfterParents.Count)
                {
                    return false;  // Speedup that will prune most comparisons
                }
                if (other.Data != Data) return false;

                return ListOfSetEquals(Before, other.Before) &&
                    ListOfSetEquals(BeforeParent, other.BeforeParent) &&
                    ListOfSetEquals(After, other.After) &&
                    ListOfSetEquals(AfterParents, other.AfterParents);
            }
            return false;
        }
    }

    public static class SplitClusterOnNode
    {
        public static IEnumerable<SublatticeModification<T>> ProposeSplits<T>(HashSet<LatticeNode<T>> nodesInType, HashSet<LatticeNode<T>> typeParents)
        {
            return nodesInType.AsParallel().Select(n => SplitOn(nodesInType, typeParents, n)).Where(m => m != null).Where(mod => mod.After.All(g => g.Count > 0));
        }

        /// <summary>
        /// Split on a specific node. This gets a type and splits it into
        /// two subtypes.
        /// </summary>
        /// <param name="nodesInType"></param>
        /// <param name="typeParents"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        public static SublatticeModification<T> SplitOn<T>(HashSet<LatticeNode<T>> nodesInType, HashSet<LatticeNode<T>> typeParents, LatticeNode<T> node)
        {
            //Debug.Assert(typeParents.Except(nodesInType).Count() == 0);

            HashSet<LatticeNode<T>> transitiveClosuresForNewType = AbstractColoredLattice<T>.GetTransitiveChildrenClosure(nodesInType, node);
            var modification = new SublatticeModification<T>()
            {
                Type = ModificationType.Split,
                Data = node,
                Before = new List<HashSet<LatticeNode<T>>>() { nodesInType },
                BeforeParent = new List<HashSet<LatticeNode<T>>>() { typeParents },
                After = new List<HashSet<LatticeNode<T>>>() {
                    new HashSet<LatticeNode<T>>(nodesInType.Except(transitiveClosuresForNewType)),
                    new HashSet<LatticeNode<T>>(transitiveClosuresForNewType)
                },
                AfterParents = new List<HashSet<LatticeNode<T>>>()
                {
                    new HashSet<LatticeNode<T>>(typeParents.Except(transitiveClosuresForNewType)),
                    new HashSet<LatticeNode<T>>(transitiveClosuresForNewType.Where(n=>typeParents.Contains(n) || n.Parents.Any(p=>!transitiveClosuresForNewType.Contains(p))).Concat(new List<LatticeNode<T>>(){ node }))
                }
            };

            // Does this split a cycle?
            var parentCluster = modification.After[0];
            var childrenCluster = modification.After[1];

            Debug.Assert(parentCluster.Intersect(childrenCluster).Count() == 0);
            Debug.Assert(modification.AfterParents[0].Except(parentCluster).Count() == 0);
            Debug.Assert(modification.AfterParents[1].Except(childrenCluster).Count() == 0);

            var parClosure = AbstractColoredLattice<T>.GetTransitiveParentClosure(parentCluster);
            parClosure.IntersectWith(childrenCluster);
            if (parClosure.Count != 0) return null; // We are trying to split a circle

            return modification;
        }

    }

    public static class MergeTwoClusters
    {
        public static IEnumerable<SublatticeModification<T>> GetAllPossibleMerges<T>(List<HashSet<LatticeNode<T>>> nodeColorings,
            List<HashSet<LatticeNode<T>>> typeParents)
        {
            // First find all pairs of types that can be merged.
            // These are the pairs where the parents of the parents of the types belong to the same type

            for (int i = 0; i < nodeColorings.Count; i++)
            {
                var typeGrandParents = new HashSet<LatticeNode<T>>(typeParents[i].SelectMany(n => n.Parents));
                var typeChildren = new HashSet<LatticeNode<T>>(nodeColorings[i].SelectMany(n => n.Children).Where(c => !nodeColorings[i].Contains(c)));

                for (int j = 0; j < nodeColorings.Count; j++)
                {
                    if (i == j) continue;
                    var otherTypeGrandParents = new HashSet<LatticeNode<T>>(typeParents[j].SelectMany(n => n.Parents));

                    Debug.Assert(nodeColorings[i].Count > 0);
                    Debug.Assert(nodeColorings[j].Count > 0);

                    // if a type is a pure parent of another, then they can merge.
                    if ((typeGrandParents.All(p => nodeColorings[j].Contains(p)) && typeGrandParents.Count != 0) ||
                        (i < j && typeGrandParents.Count == 0 && (otherTypeGrandParents.Count == 0 || typeChildren.Count == 0)))
                    {
                        Debug.Assert(typeParents[i].All(t => nodeColorings[i].Contains(t)));
                        Debug.Assert(typeParents[j].All(t => nodeColorings[j].Contains(t)));
                        yield return new SublatticeModification<T>()
                        {
                            Type = ModificationType.Merge,
                            Before = new List<HashSet<LatticeNode<T>>>() { nodeColorings[i], nodeColorings[j] },
                            BeforeParent = new List<HashSet<LatticeNode<T>>>() { typeParents[i], typeParents[j] },
                            After = new List<HashSet<LatticeNode<T>>>() {
                                 new HashSet<LatticeNode<T>>(nodeColorings[i].Concat(nodeColorings[j]))
                             },
                            AfterParents = new List<HashSet<LatticeNode<T>>>() { typeParents[j] }
                        };
                    }
                    
                    if (typeGrandParents.Count == 0 || otherTypeGrandParents.Count == 0) continue;
                    if (i < j) continue;

                    for (int k = 0; k < nodeColorings.Count; k++)
                    {
                        if (i == k || j == k) continue;
                        // if the i'th and j'th grandparents belong to the same type or to each other, they can be merged
                        var jointGrandParents = typeGrandParents.Concat(otherTypeGrandParents).Distinct();
                        if (jointGrandParents.All(p => nodeColorings[k].Contains(p) || nodeColorings[j].Contains(p) || nodeColorings[i].Contains(p)))
                        {
                            Debug.Assert(typeParents[i].All(t => nodeColorings[i].Contains(t)));
                            Debug.Assert(typeParents[j].All(t => nodeColorings[j].Contains(t)));
                            yield return new SublatticeModification<T>()
                            {
                                Type = ModificationType.Merge,
                                Before = new List<HashSet<LatticeNode<T>>>() { nodeColorings[i], nodeColorings[j] },
                                BeforeParent = new List<HashSet<LatticeNode<T>>>() { typeParents[i], typeParents[j] },
                                After = new List<HashSet<LatticeNode<T>>>() {
                                  new HashSet<LatticeNode<T>>(nodeColorings[i].Concat(nodeColorings[j]))
                                },
                                AfterParents = new List<HashSet<LatticeNode<T>>>() {
                                    new HashSet<LatticeNode<T>>(typeParents[j].Concat(typeParents[i]).Where(
                                        n=>n.Parents.Any(p=> !nodeColorings[i].Contains(p) && !nodeColorings[j].Contains(p))))
                                }
                            };
                        }
                    }

                }
            }
        }
    }
}
