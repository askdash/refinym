using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SubtypeInduction.TypeSystemRels
{
    public class ClusteringExtractor
    {
        private readonly AbstractColoredLattice<NodeName> _lattice;
        private readonly IVariationOfInformationComputer<NodeName> _ciComputer;
        private readonly Dictionary<AbstractNode, LatticeNode<NodeName>> _nodeMap;
        public int NumRelationships { get; private set; }

        public ClusteringExtractor(HashSet<string> types, TypeConstraints collectedConstraints)
        {
            collectedConstraints.RemoveSelfLinks();

            _ciComputer = new SubtokenVariationOfInformationComputer(dirichletAlpha: 10);
            _lattice = new SuperGreedySplitingVIColoredLattice<NodeName>(_ciComputer);

            // TODO: Allow external choice of splitting method
            Func<AbstractNode, string[]> subtokenSplitting = n => SubtokenSplitter.SplitSubtokens(n.Name).ToArray();
            Func<AbstractNode, string[]> charSplitting = n => n.Name.ToLower().Select(ch => ch.ToString()).ToArray();
            Func<AbstractNode, string[]> bigramSplitting = n =>
            {
                if (n.Name.Length == 0) return new string[] { "" };
                var name = n.Name.ToLower();
                return Enumerable.Range(0, name.Length - 1).Select(i => name.Substring(i, 2)).ToArray();
            };
            Func<AbstractNode, string[]> subtokenBigramSplitting = n =>
            {
                return SubtokenSplitter.SplitSubtokens(n.Name).SelectMany(sub =>
                {
                    return Enumerable.Range(0, sub.Length - 1).Select(i => sub.Substring(i, 2));
                }).ToArray();

            };
            Func<AbstractNode, string[]> subtokenTrigramSplitting = n =>
            {
                return SubtokenSplitter.SplitSubtokens(n.Name).SelectMany(sub =>
                {
                    if (sub.Length < 3) return new string[] { sub };
                    return Enumerable.Range(0, sub.Length - 2).Select(i => sub.Substring(i, 3));
                }).ToArray();
            };

            Func<AbstractNode, string[]> trigramAndSubtokenSplitting = n => subtokenSplitting(n).Concat(subtokenTrigramSplitting(n)).ToArray();

            _nodeMap = _lattice.Add(collectedConstraints.AllRelationships.Where(kv => types.Contains(kv.Key.Type)).
                ToDictionary(kv => kv.Key, kv => new HashSet<AbstractNode>(kv.Value.Where(n => types.Contains(n.Type)))),
                s => new NodeName(subtokenSplitting(s)));
            NumRelationships = _lattice.NumRelationships;
        }

        public (List<HashSet<AbstractNode>> Clusters, List<HashSet<int>> ClusterParents) InferColors()
        {
            var coloring = _lattice.InferColoring(out double score);
            Console.WriteLine($"Color infrence completed. Score: {score}");

            // Convert to AbstractNode
            var inverseMap = _nodeMap.ToDictionary(kv => kv.Value, kv => kv.Key);
            var colorGroups = coloring.Select(g => new HashSet<AbstractNode>(g.Select(n => inverseMap[n]))).ToList();

            var parents = new List<HashSet<int>>();
            foreach (var color in coloring)
            {
                var nodesInColor = new HashSet<LatticeNode<NodeName>>(color);
                var parentNodes = new HashSet<LatticeNode<NodeName>>(color.SelectMany(n => n.Parents).Where(n => !nodesInColor.Contains(n)));
                var parentColorIds = new HashSet<int>();
                foreach (var parentNode in parentNodes)
                {
                    for (int i = 0; i < coloring.Count; i++)
                    {
                        if (coloring[i].Contains(parentNode))
                        {
                            parentColorIds.Add(i);
                            break;
                        }
                    }
                }
                parents.Add(parentColorIds);
            }
            return (colorGroups, parents);
        }

        public static void ExtractFromSolution(string repositoryPath, string githubPath, Solution solution,
            string saveDir, string typeToCluster="string")
        {
            Func<string, int, string> pathProcessor = (fullPath, lineNumber) =>
            {
                var basePath = repositoryPath;
                var relativePath = fullPath.Substring(basePath.Length);
                var githubLink = githubPath + relativePath.Replace('\\', '/') + "#L" + (lineNumber + 1);
                return githubLink;
            };

            Console.WriteLine("Collecting type constraint graph...");
            var typeRelations = new TypeConstraints(pathProcessor);

            var projectGraph = solution.GetProjectDependencyGraph();
            var compilations = new List<CSharpCompilation>();

            foreach (var projectId in projectGraph.GetTopologicallySortedProjects())
            {
                Compilation compilation;
                try
                {
                    var project = solution.GetProject(projectId);
                    if (project.FilePath.ToLower().Contains("test"))
                    {
                        Console.WriteLine($"Excluding {project.FilePath} since it seems to be test-related");
                        continue;
                    }
                    compilation = project.GetCompilationAsync().Result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception while compiling project {0}: {1}", projectId, ex);
                    continue;
                }
                foreach (var error in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.WriteLine(error.GetMessage());
                }
                if (compilation is CSharpCompilation cSharpCompilation)
                {
                    typeRelations.AddFromCompilation(cSharpCompilation);
                    compilations.Add(cSharpCompilation);
                }
            }

            var extractor = new ClusteringExtractor(new HashSet<string> { typeToCluster }, typeRelations);
            var (clusters, clusterParents) = extractor.InferColors();
            ClusteringSerializerUtil.SerializeClustering(saveDir, repositoryPath, clusters, clusterParents);
        }
    }
}
