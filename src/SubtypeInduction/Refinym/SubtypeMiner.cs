using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UDCTreeNamespace;
using GodClassEvaluation;



namespace SubtypeInduction.TypeSystemRels
{
    using ClassSpecificRelationships = Dictionary<string, IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>>>;

    public class SubtypeMiner
    {
        private readonly AbstractColoredLattice<NodeName> _lattice;
        private readonly IVariationOfInformationComputer<NodeName> _ciComputer;
        private readonly Dictionary<AbstractNode, LatticeNode<NodeName>> _nodeMap;
        public int NumRelationships { get; private set; }
        public Dictionary<ITypeSymbol, KeyValuePair<AbstractNode, HashSet<AbstractNode>>> UDTLocations;
        public Dictionary<int, List<int>> AncestorMap { get; set; } = new Dictionary<int, List<int>>();

        enum AnalysisType { UDCSanityCheck, TypeSpecificFlowClustering, Rewriting, RewritingFromSerialization };

        public SubtypeMiner(HashSet<string> types, TypeConstraints collectedConstraints, int maxNumTypes, bool UDTSpecificAnalysis = false, ITypeSymbol t = null)
        {
            collectedConstraints.RemoveSelfLinks();

            _ciComputer = new SubtokenVariationOfInformationComputer(dirichletAlpha: 2);
            _lattice = new SuperGreedySplitingVIColoredLattice<NodeName>(_ciComputer);

            // TODO: Allow external choice of splitting type
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

            if (UDTSpecificAnalysis)
            {
                /*IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>> nodes = collectedConstraints.AllRelationships.Where(kv => kv.Key.IsSymbol);
                var symbols = nodes.Where(kv => (kv.Key as VariableSymbol) !=null);*/

                var symbols = GetTypeSpecificRelations(collectedConstraints.AllRelationships, t);

                _nodeMap = _lattice.Add(
                    symbols.ToDictionary(
                        kv => kv.Key,
                        kv => new HashSet<AbstractNode>(kv.Value)
                    ),
                    s => new NodeName(subtokenSplitting(s))
                );
            }
            else
            {
                var selKeys = collectedConstraints.AllRelationships.Where(kv => types.Contains(kv.Key.Type));
                _nodeMap = _lattice.Add(selKeys.
                ToDictionary(kv => kv.Key, kv => new HashSet<AbstractNode>(kv.Value.Where(n => types.Contains(n.Type)))),
                s => new NodeName(subtokenSplitting(s)));
            }
            NumRelationships = _lattice.NumRelationships;
        }

        private bool CheckAbstractNodeInType(AbstractNode n, ITypeSymbol t)
        {
            if (t.Locations.Count() == 0) return false;
            string typePath = t.Locations.First().GetMappedLineSpan().Path;
            string nodePath = n.Location.GetMappedLineSpan().Path;

            var foo = t.Locations.First().SourceTree.GetRoot();

            var node = t.Locations.First().SourceTree.GetRoot().FindNode(t.Locations.First().SourceSpan);
            var typeStartLine = node.SyntaxTree.GetLineSpan(node.FullSpan).StartLinePosition.Line;
            var typeEndLine = node.SyntaxTree.GetLineSpan(node.FullSpan).EndLinePosition.Line;


            /*int typeStartLine = t.Locations.First().GetMappedLineSpan().StartLinePosition.Line;
            int typeEndLine = t.Locations.First().GetMappedLineSpan().EndLinePosition.Line;*/

            int nodeStartLine = n.Location.GetMappedLineSpan().StartLinePosition.Line;
            int nodeEndLine = n.Location.GetMappedLineSpan().EndLinePosition.Line;

            return (nodePath == typePath &&
                    nodeStartLine >= typeStartLine &&
                    nodeEndLine <= typeEndLine);
        }

        public int GetLineSpanOfType(ITypeSymbol t)
        {
            var node = t.Locations.First().SourceTree.GetRoot().FindNode(t.Locations.First().SourceSpan);
            var typeStartLine = node.SyntaxTree.GetLineSpan(node.FullSpan).StartLinePosition.Line;
            var typeEndLine = node.SyntaxTree.GetLineSpan(node.FullSpan).EndLinePosition.Line;
            return typeEndLine - typeStartLine + 1;
        }

        private IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>> GetTypeSpecificRelations(
            IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>> relationships,
            ITypeSymbol t)
        {

            IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>> nodes_sourcefiltered =
                relationships.Where(kv => CheckAbstractNodeInType(kv.Key, t));
            
            IEnumerable<KeyValuePair<AbstractNode, HashSet<AbstractNode>>> nodes_sinkfiltered =
                nodes_sourcefiltered.Select(kv =>
                    new KeyValuePair<AbstractNode, HashSet<AbstractNode>>(
                        kv.Key,
                        new HashSet<AbstractNode>(
                            kv.Value.Where(
                                v => CheckAbstractNodeInType(v, t)
                            )
                        )
                    )
                );

            return nodes_sinkfiltered;
        }

        public List<double> ComputePurities(List<HashSet<AbstractNode>> result)
        {
            int total = 0;
            int mostFrequentCount = 0;
            List<double> purities = new List<double>();
            foreach (var colour in result)
            {
                total = colour.Count();
                var groupedColours = colour.GroupBy(i => i.Type);
                mostFrequentCount = groupedColours.Max(i => i.Count());
                if (total > 0)
                {
                    purities.Add((double)mostFrequentCount / (double)total);
                }
                else
                {
                    purities.Add(0.0);
                }
            }
            return purities;
        }

        public (List<HashSet<AbstractNode>> Clusters, List<HashSet<int>> ClusterParents) InferColors()
        {
            var coloring = _lattice.InferColoring(out double score);
            Console.WriteLine($"Color infrence completed. Score: {score}");

            // Convert to AbstractNode
            var inverseMap = _nodeMap.ToDictionary(kv => kv.Value, kv => kv.Key);
            var colorGroups = coloring.Select(g => new HashSet<AbstractNode>(g.Select(n => inverseMap[n]))).ToList();

            var parents = new List<HashSet<int>>();
            int colorID = 0;

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
                var ancestors = AbstractColoredLattice<NodeName>.GetClusterAncestors(color, coloring);
                var ancestorIDs = ancestors.Select(i => coloring.IndexOf(i)).ToList();
                AncestorMap.Add(colorID++, ancestorIDs);
            }
            return (colorGroups, parents);
        }

        public static void ExtractFromSolution(string basePath, string githubBaseLink, Solution solution)
        {
            Func<string, int, string> pathProcessor = (fullPath, lineNumber) =>
            {
                var relativePath = fullPath.Substring(basePath.Length);
                return githubBaseLink + relativePath.Replace('\\', '/') + "#L" + (lineNumber + 1);
            };


            Console.WriteLine("Collecting type constraint graph...");
            var typeRelations = new TypeConstraints(pathProcessor);

            var projectGraph = solution.GetProjectDependencyGraph();
            UDCTree UDCHierarchy = new UDCTree();
            var compilations = new List<CSharpCompilation>();

            foreach (var projectId in projectGraph.GetTopologicallySortedProjects())
            {
                Compilation compilation;
                try
                {
                    var project = solution.GetProject(projectId);
                    if (project.FilePath.ToLower().Contains("test")
                        || !(project.FilePath.ToLower().EndsWith(".csproj")))
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
                if (compilation == null) continue;
                foreach (var error in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.WriteLine(error.GetMessage());
                }
                if (compilation is CSharpCompilation cSharpCompilation)
                {
                    UDCHierarchy.ParseTypesInCompilation(cSharpCompilation);
                    typeRelations.AddFromCompilation(cSharpCompilation);
                    compilations.Add(cSharpCompilation);
                }
            }

            //AnalysisType t = AnalysisType.TypeSpecificFlowClustering;

            var analysisTriggers = new List<AnalysisType> { 
                //AnalysisType.UDCSanityCheck, 
                //AnalysisType.TypeSpecificFlowClustering, 
                AnalysisType.RewritingFromSerialization,
                };

            foreach (var i in analysisTriggers)
            {
                switch (i)
                {
                    case AnalysisType.TypeSpecificFlowClustering:
                        var TSFresults_path = "results/" + "TypeSpecificClustering/" + basePath.Substring(basePath.LastIndexOf('\\') + 1);
                        Console.WriteLine("Starting coloring for name flows in individual user defined types...");
                        GodClassResults typeSpecificClustering = new GodClassResults(pathProcessor, typeRelations, TSFresults_path);
                        typeSpecificClustering.startTypeSpecificNameFlowInference(UDCHierarchy, 0);
                        break;

                    case AnalysisType.UDCSanityCheck:
                        var sanityResults_path = "results/" + "UDCSanityCheck/" + basePath.Substring(basePath.LastIndexOf('\\') + 1);
                        Console.WriteLine("Starting coloring for sanity checking on user defined types...");
                        GodClassResults sanityCheck = new GodClassResults(pathProcessor, typeRelations, sanityResults_path);
                        sanityCheck.startConsolidatedInference(UDCHierarchy, 0);
                        break;

                    case AnalysisType.Rewriting:
                        var rewritingResults_path = "results/" + "rewriting/" + basePath.Substring(basePath.LastIndexOf('\\') + 1);
                        Console.WriteLine("Starting rewriting of builtin type uses...");
                        EvaluateOnBuiltIns(compilations, pathProcessor, typeRelations, rewritingResults_path, false);
                        break;

                    case AnalysisType.RewritingFromSerialization:
                        var rewritingFromSerializationResults_path = "results/" + "rewritingFromSerialised/" + basePath.Substring(basePath.LastIndexOf('\\') + 1);
                        Console.WriteLine("Starting rewriting of builtin type uses after reading in serilised clusterings...");
                        EvaluateOnBuiltIns(compilations, pathProcessor, typeRelations, rewritingFromSerializationResults_path, true,
                            basePath.Substring(basePath.LastIndexOf('\\') + 1));
                        break;

                    default:
                        break;
                }
            }
            Console.WriteLine("Done!");
        }

        public static void Rewrite(string basePath, string githubBaseLink, Solution solution,
            string serializedClustersFile, string targetTypeName)
        {
            Func<string, int, string> pathProcessor = (fullPath, lineNumber) =>
            {
                var relativePath = fullPath.Substring(basePath.Length);
                return githubBaseLink + relativePath.Replace('\\', '/') + "#L" + (lineNumber + 1);
            };

            Console.WriteLine("Collecting type constraint graph...");
            var typeRelations = new TypeConstraints(pathProcessor);

            var projectGraph = solution.GetProjectDependencyGraph();
            UDCTree UDCHierarchy = new UDCTree();
            var compilations = new List<CSharpCompilation>();

            foreach (var projectId in projectGraph.GetTopologicallySortedProjects())
            {
                Compilation compilation;
                try
                {
                    var project = solution.GetProject(projectId);
                    if (project.FilePath.ToLower().Contains("test")
                        || !(project.FilePath.ToLower().EndsWith(".csproj")))
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
                if (compilation == null) continue;
                foreach (var error in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.WriteLine(error.GetMessage());
                }
                if (compilation is CSharpCompilation cSharpCompilation)
                {
                    UDCHierarchy.ParseTypesInCompilation(cSharpCompilation);
                    typeRelations.AddFromCompilation(cSharpCompilation);
                    compilations.Add(cSharpCompilation);
                }
            }

            Console.WriteLine("Starting rewriting of builtin type uses after reading in serialized clusterings...");
            RewriterFromJson rJson = new RewriterFromJson(serializedClustersFile, basePath, typeRelations);
            var r = new Rewriter(compilations, rJson.Clusters, rJson.AncestorMap, targetTypeName);

            r.RewriteTypes();
            string errors = JsonConvert.SerializeObject(r.ErrorHistogram, Formatting.Indented);
            Console.WriteLine(errors);
        }

        public static void EvaluateOnBuiltIns(
            List<CSharpCompilation> compilations, Func<string, int, string> pathProcessor,
            TypeConstraints typeRelations, string results_path, bool deserializeClusters = false,
            string slnName = null)
        {
            Dictionary<string, Tuple<string, string>> serialisedClusters = new Dictionary<string, Tuple<string, string>>();

            void bootstrapSerialisedClusters()
            {
                serialisedClusters["BEPUphysics.sln"] = new Tuple<string, string>(@"E:\bepuphysics1", @"F:\clusterings\bepuphysics-float.json");
                serialisedClusters["Microsoft.Bot.Builder.sln"] = new Tuple<string, string>(@"E:\BotBuilder\CSharp\", @"F:\clusterings\botbuilder-str.json");
                serialisedClusters["CommandLine.sln"] = new Tuple<string, string>(@"E:\commandline\", @"F:\clusterings\commandline-str.json");
                serialisedClusters["CommonMark.sln"] = new Tuple<string, string>(@"E:\CommonMark.NET\", @"F:\clusterings\commonmark-str.json");
                serialisedClusters["Hangfire.sln"] = new Tuple<string, string>(@"E:\Hangfire\", @"F:\clusterings\hangfire-str.json");
                serialisedClusters["Humanizer.sln"] = new Tuple<string, string>(@"E:\Humanizer\", @"F:\clusterings\humanizer-str.json");
                serialisedClusters["QuantConnect.Lean.sln"] = new Tuple<string, string>(@"E:\Lean\", @"F:\clusterings\lean-str.json");
                serialisedClusters["Nancy.sln"] = new Tuple<string, string>(@"E:\Nancy\", @"F:\clusterings\nancy-str.json");
                serialisedClusters["Newtonsoft.Json.Net40.sln"] = new Tuple<string, string>(@"E:\Newtonsoft.Json\", @"F:\clusterings\newtonsoft-str.json");
                serialisedClusters["Ninject.sln"] = new Tuple<string, string>(@"E:\Ninject\", @"F:\clusterings\ninject-str.json");
                serialisedClusters["NLog.sln"] = new Tuple<string, string>(@"E:\NLog\", @"F:\clusterings\nlog-str.json");
                serialisedClusters["Quartz.sln"] = new Tuple<string, string>(@"E:\quartznet\", @"F:\clusterings\quartznet-str.json");
                serialisedClusters["RavenDB.sln"] = new Tuple<string, string>(@"E:\ravendb\", @"F:\clusterings\ravendb-str.json");
                serialisedClusters["RestSharp.sln"] = new Tuple<string, string>(@"E:\RestSharp\", @"F:\clusterings\restsharp-str.json");
                serialisedClusters["Wox.sln"] = new Tuple<string, string>(@"E:\Wox\", @"F:\clusterings\wox-str.json");
            }

            bootstrapSerialisedClusters();

            string typename = "string";
            UDCTree hierarchy = null;
            foreach (var compilation in compilations)
            {
                ITypeSymbol t = compilation.GetTypeByMetadataName(typename);
                if (t != null)
                {
                    hierarchy = new UDCTree(t);
                    break;
                }
            }

            Rewriter r;
            if (deserializeClusters)
            {
                var serializedClustersFile = serialisedClusters[slnName].Item2;
                var repoPath = serialisedClusters[slnName].Item1;
                RewriterFromJson rJson = new RewriterFromJson(serializedClustersFile, repoPath, typeRelations);
                r = new Rewriter(compilations, rJson.Clusters, rJson.AncestorMap, typename);
            }
            else
            {
                GodClassResults results = new GodClassResults(pathProcessor, typeRelations, results_path);
                results.startInference(hierarchy);
                r = new Rewriter(compilations, results.MiningResults.First().clusteringResult, results.MiningResults.First().ancestorMap, typename);
            }

            r.RewriteTypes();
            string errors = JsonConvert.SerializeObject(r.ErrorHistogram, Formatting.Indented);
            try
            {
                if (!Directory.Exists(results_path))
                {
                    Directory.CreateDirectory(results_path);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                System.Environment.Exit(-1);
            }

            System.IO.DirectoryInfo di = new DirectoryInfo(results_path);

            foreach (FileInfo f in di.GetFiles())
            {
                f.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            System.IO.StreamWriter file = new System.IO.StreamWriter(results_path + "/" + typename + "_rewriting_errors.txt");
            file.WriteLine(errors);
            file.Close();
        }
    }
}
