using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SubtypeInduction.TypeSystemRels
{
    /// <summary>
    /// An extremely greedy version, that picks the first option that gives positive VI improvement.
    /// </summary>
    public class SuperGreedySplitingVIColoredLattice<T> : VariationOfInformationColoredLattice<T>
    {
        public SuperGreedySplitingVIColoredLattice(IVariationOfInformationComputer<T> viComputer) : base(viComputer)
        {
        }

        public override List<HashSet<LatticeNode<T>>> InferColoring(out double totalViImprovement, int maxNumIterations = 2500,
            int clusteringTimoutMinutes = 2 * 24 * 60)
        {
            _viComputer.CacheGlobalInformation(_allNodes);
            var numTotalNodes = _viComputer.NumEffectiveNodes(_allNodes);
            RemoveSelfLoops(_allNodes);

            Console.WriteLine("Retrieving Independent Components...");
            List<HashSet<LatticeNode<T>>> currentColoring = GetLatticeIdependentComponents();

            // Remove all "empty" clusters.
            currentColoring = currentColoring.Where(c => _viComputer.NumEffectiveNodes(c) > 0).ToList();

            Console.WriteLine("Retrieving Component Parents...");
            List<HashSet<LatticeNode<T>>> currentColoringParents = currentColoring
                .Select(g => new HashSet<LatticeNode<T>>(GetParentNodesForType(g))).ToList();

            Console.WriteLine("Starting Greedy Optimization...");
            Debug.Assert(!ClustersAreCyclic(currentColoring));

            totalViImprovement = 0;
            var rng = new Random();
            int iterations = 0;
            var startTime = DateTime.Now;
            while (true)
            {
                Console.WriteLine($"VI Cache Hit Rate {CacheHitRate * 100: 00.0}%");
                if (iterations++ > maxNumIterations)
                {
                    Console.WriteLine("Reached maximum number of iterations.");
                    break;
                }
                if (DateTime.Now - startTime > TimeSpan.FromMinutes(clusteringTimoutMinutes))
                {
                    Console.WriteLine("Clustering Timed Out.");
                    break;
                }
                if (rng.NextDouble() < .1) // Randomize with some probability to take advantage of caching.
                {
                    var randomOrder = Enumerable.Range(0, currentColoring.Count).Select(e => rng.NextDouble()).ToArray();
                    currentColoring = Enumerable.Range(0, currentColoring.Count).Select(i => (i, currentColoring[i])).OrderBy(el => randomOrder[el.Item1]).Select(el => el.Item2).ToList();
                    currentColoringParents = Enumerable.Range(0, currentColoringParents.Count).Select(i => (i, currentColoringParents[i])).OrderBy(el => randomOrder[el.Item1]).Select(el => el.Item2).ToList();
                }


                double bestImprovement = 0;
                SublatticeModification<T> bestMerge = null;
                foreach ((SublatticeModification<T> potentialMerge, double viImprovement) in
                    MergeTwoClusters.GetAllPossibleMerges(currentColoring, currentColoringParents).AsParallel()
                    .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                    .Select(m => (m, SpeculativeModificationVariationOfInfoGain(m, numTotalNodes))))
                {
                    if (viImprovement > bestImprovement)
                    {
                        bestMerge = potentialMerge;
                        bestImprovement = viImprovement;
                        break;
                    }
                }

                if (bestImprovement > 0.0)
                {
                    Console.WriteLine($"{iterations} (num clusters {currentColoringParents.Count}): Lattice Merge with VI improvement: {bestImprovement}");
                    ApplyModification(currentColoring, currentColoringParents, bestMerge);
                    Debug.Assert(!ClustersAreCyclic(currentColoring));
                    totalViImprovement += bestImprovement;
                    continue;
                }

                bestImprovement = 0;
                SublatticeModification<T> bestSplit = null;
                // Take advantage of the AsParallel to compute the gain in parallel
                foreach ((SublatticeModification<T> potentialSplit, double viImprovement) in
                    ProposeSplitModification(currentColoring, currentColoringParents).AsParallel()
                    .WithMergeOptions(ParallelMergeOptions.NotBuffered)
                    .Select(m => (m, SpeculativeModificationVariationOfInfoGain(m, numTotalNodes))))
                {
                    if (viImprovement > bestImprovement)
                    {
                        bestImprovement = viImprovement;
                        bestSplit = potentialSplit;
                        break;
                    }
                }

                if (bestImprovement > 0.0)
                {
                    Console.WriteLine($"{iterations} (num clusters {currentColoringParents.Count}): Lattice Split with VI improvement: {bestImprovement}");
                    ApplyModification(currentColoring, currentColoringParents, bestSplit);
                    Debug.Assert(!ClustersAreCyclic(currentColoring));
                    totalViImprovement += bestImprovement;
                    continue;
                }

                break;
            }
            return currentColoring;
        }

        private void RemoveSelfLoops(HashSet<LatticeNode<T>> allNodes)
        {
            foreach (var node in allNodes)
            {
                if (node.Parents.Contains(node))
                {
                    node.Parents.RemoveAll(n => n == node);
                }
                if (node.Children.Contains(node))
                {
                    node.Children.RemoveAll(n => n == node);
                }
            }
        }
    }
}
