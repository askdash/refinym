using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SubtypeInduction.TypeSystemRels
{
    public abstract class VariationOfInformationColoredLattice<T> : AbstractColoredLattice<T>
    {
        protected readonly IVariationOfInformationComputer<T> _viComputer;

        public VariationOfInformationColoredLattice(IVariationOfInformationComputer<T> computer)
        {
            _viComputer = computer;
        }

        public double ComputeVariationOfInformation(List<HashSet<LatticeNode<T>>> nodeGrouping)
        {
            int totalNumNodes = _viComputer.NumEffectiveNodes(nodeGrouping.SelectMany(n => n));
            return _viComputer.ComputeVariationOfInformation(nodeGrouping, totalNumNodes);
        }

        public class SetEqualityComparer : IEqualityComparer<HashSet<LatticeNode<T>>>
        {
            public bool Equals(HashSet<LatticeNode<T>> x, HashSet<LatticeNode<T>> y)
            {
                return x.SetEquals(y);
            }

            public int GetHashCode(HashSet<LatticeNode<T>> obj)
            {
                int hashCode = 0;
                foreach (var o in obj)
                {
                    hashCode += o.GetHashCode();
                }
                return hashCode;
            }
        }

        private struct Modification
        {
            public static readonly SetEqualityComparer _comparer = new SetEqualityComparer();

            public Modification(List<HashSet<LatticeNode<T>>> mod)
            {
                Data = mod;
            }

            public readonly List<HashSet<LatticeNode<T>>> Data;

            public override int GetHashCode()
            {
                return Data[0].GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is Modification mod)
                {
                    return mod.Data.SequenceEqual(Data);
                }
                return false;
            }
        }

        class VIValue
        {
            public VIValue(double v)
            {
                Value = v;
            }

            public readonly double Value;
        }

        private readonly LRUCache<(ModificationType, Modification, object), double> _viCache = new LRUCache<(ModificationType, Modification, object), double>(50000);

        public double CacheHitRate => _viCache.HitRate();

        /// <summary>
        /// Compute the improvement of VI for the given split. The larger the better.
        /// </summary>
        /// <param name="latticeModification"></param>
        /// <param name="totalNumNodes"></param>
        /// <param name="subtokenBaseProbDist"></param>
        /// <param name="baseNameProbDist"></param>
        /// <returns></returns>
        public double SpeculativeModificationVariationOfInfoGain(SublatticeModification<T> latticeModification, int totalNumNodes)
        {
            return _viCache.Get((latticeModification.Type, new Modification(latticeModification.Before), latticeModification.Data), () =>
            {
                // Compute before score
                double scoreBefore = _viComputer.ComputeVariationOfInformation(latticeModification.Before, totalNumNodes);

                // Compute after score
                double scoreAfter = _viComputer.ComputeVariationOfInformation(latticeModification.After, totalNumNodes);

                //Return difference
                return scoreBefore - scoreAfter;
            });
        }

    }

    public interface IVariationOfInformationComputer<TNodeData>
    {
        void CacheGlobalInformation(IEnumerable<LatticeNode<TNodeData>> nodes);

        double ComputeVariationOfInformation(List<HashSet<LatticeNode<TNodeData>>> nodeSubGrouping, int numTotalNodes);

        int NumEffectiveNodes(IEnumerable<LatticeNode<TNodeData>> allNodes);
    }

    public class SubtokenVariationOfInformationComputer : IVariationOfInformationComputer<NodeName>
    {

        private readonly double _dirichletAlpha;

        public SubtokenVariationOfInformationComputer(double dirichletAlpha)
        {
            _dirichletAlpha = dirichletAlpha;
        }

        private MultinomialDistribution<string> _baseSubtokenProbDist = null;

        void IVariationOfInformationComputer<NodeName>.CacheGlobalInformation(IEnumerable<LatticeNode<NodeName>> nodes)
        {
            _baseSubtokenProbDist = SubtokenProbDist(nodes.SelectMany(n => n.Data));
        }

        int IVariationOfInformationComputer<NodeName>.NumEffectiveNodes(IEnumerable<LatticeNode<NodeName>> allNodes) => allNodes.Count(n => n.Data.Length > 0);

        public MultinomialDistribution<string> GetParentNameDistribution(HashSet<LatticeNode<NodeName>> group,
            MultinomialDistribution<string> baseDistribution,
            double distanceDiscount = .9, double tolerance = 10e-4)
        {
            var distribution = new MultinomialDistribution<string>();
            // Add minimally the base distribution to avoid NaNs
            foreach (var subtoken in baseDistribution.Elements)
            {
                distribution.Add(subtoken, (decimal)(tolerance * baseDistribution.ProbabilityOf(subtoken)));
            }


            var visited = new HashSet<LatticeNode<NodeName>>(group);
            var toVisit = new Stack<(LatticeNode<NodeName>, int)>();
            foreach (var parentNode in group.SelectMany(n => n.Parents).Where(n => !visited.Contains(n)))
            {
                toVisit.Push((parentNode, 1));
            }
            while (toVisit.Count > 0)
            {
                (var nextNode, var depth) = toVisit.Pop();

                visited.Add(nextNode);
                decimal countAs = (decimal)(Math.Pow(distanceDiscount, depth));
                foreach (var subtoken in nextNode.Data)
                {
                    distribution.Add(subtoken, countAs);
                }

                foreach (var parentNode in nextNode.Parents.Where(n => !visited.Contains(n)))
                {
                    toVisit.Push((parentNode, depth + 1));
                }
            }
            return distribution;
        }

        /// <summary>
        /// Compute VI using pre-computed information (that can be cashed across multiple evaluations
        /// </summary>
        /// <param name="nodeSubGrouping"></param>
        /// <param name="numTypes"></param>
        /// <param name="baseNameProbDist"></param>
        /// <returns></returns>
        double IVariationOfInformationComputer<NodeName>.ComputeVariationOfInformation(List<HashSet<LatticeNode<NodeName>>> nodeSubGrouping, int numTotalNodes)
        {
            Debug.Assert(_baseSubtokenProbDist != null);
            var subtokenDistrPerGroup = nodeSubGrouping.Select(g => SubtokenProbDist(g.Where(n => n.Data.Length > 0).SelectMany(n => n.Data))).ToList();  // P(subtoken|type)

            // Compute H(subtoken|type)
            double subtokenGivenTypeConditionalEntropy = 0;
            int idx = 0;
            foreach (var typeGroup in nodeSubGrouping)
            {
                var subtokenDistrForGroup = subtokenDistrPerGroup[idx];
                MultinomialDistribution<string> typeGroupParentProb = null;
                if (_dirichletAlpha > 0)
                {
                    typeGroupParentProb = GetParentNameDistribution(typeGroup, _baseSubtokenProbDist);
                }

                double probType = ((double)typeGroup.Where(n => n.Data.Length > 0).Count()) / numTotalNodes;

                var subtokenEntropy = subtokenDistrForGroup.Elements
                    .Select(e => subtokenDistrForGroup.ProbabilityOf(e, typeGroupParentProb, _dirichletAlpha))
                    .Select(p => -p * Math.Log(p)).Sum();
                subtokenGivenTypeConditionalEntropy += probType * subtokenEntropy;
                idx++;
            }

            // Compute H(type|subtokens)
            double typeGivenSubtokenConditionalEntropy = 0;
            foreach (var subtoken in _baseSubtokenProbDist.Elements)
            {
                var baseProbSubtoken = _baseSubtokenProbDist.ProbabilityOf(subtoken);

                var entropyOfTypeGivenSubtoken = subtokenDistrPerGroup
                    .Select(g =>
                    {
                        return ((double)g[subtoken]) / (double)_baseSubtokenProbDist[subtoken];
                    })
                    .Where(p => p != 0)
                    .Select(p => -p * Math.Log(p)).Sum();

                typeGivenSubtokenConditionalEntropy += baseProbSubtoken * entropyOfTypeGivenSubtoken;
            }

            Debug.Assert(subtokenGivenTypeConditionalEntropy >= 0);
            Debug.Assert(typeGivenSubtokenConditionalEntropy >= 0);
            return subtokenGivenTypeConditionalEntropy + typeGivenSubtokenConditionalEntropy;
        }

        public static MultinomialDistribution<string> SubtokenProbDist(IEnumerable<string> subtokens)
        {
            var distribution = new MultinomialDistribution<string>();
            distribution.AddManyOnce(subtokens);
            return distribution;
        }

        public static double ProbName(NodeName subtokens, MultinomialDistribution<string> distribution,
            MultinomialDistribution<string> prior = null, double dirichletAlpha = .1) =>
                            CrossEntropyNameMultinomial(subtokens.Select(s => distribution.ProbabilityOf(s, prior, dirichletAlpha)).ToArray());

        public static double CrossEntropyNameMultinomial(double[] usedSubtokenProbs)
        {
            double res = 0;
            foreach (var prob in usedSubtokenProbs) res += Math.Log(prob);
            return Math.Exp(res / usedSubtokenProbs.Length);
        }
    }
    
    public class NodeName : IEnumerable<string>
    {
        public NodeName(string[] nameSubtokens)
        {
            Subtokens = nameSubtokens;
        }
        public string[] Subtokens { get; }

        public int Length => Subtokens.Length;

        public override int GetHashCode()
        {
            if (Subtokens.Length == 0) return 0;
            return Subtokens[0].GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is NodeName other)
            {
                return Subtokens.SequenceEqual(other.Subtokens);
            }
            return false;
        }

        public override string ToString()
        {
            return string.Join(",", Subtokens);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return Subtokens.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Subtokens.GetEnumerator();
        }
    }

    class GroupComparer<T> : IEqualityComparer<List<HashSet<LatticeNode<T>>>>
    {
        public bool Equals(List<HashSet<LatticeNode<T>>> x, List<HashSet<LatticeNode<T>>> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
            {
                if (!x[i].SetEquals(y[i])) return false;
            }
            return true;
        }

        public int GetHashCode(List<HashSet<LatticeNode<T>>> obj)
        {
            return obj.Count.GetHashCode();
        }
    }
}
