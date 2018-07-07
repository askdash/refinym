using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UDCTreeNamespace;
using System.IO;
using SubtypeInduction.TypeSystemRels;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace GodClassEvaluation
{
    class SubTreeResults
    {
        private class SilhouetteAssessment
        {
            private List<HashSet<AbstractNode>> _clusters;
            private List<double> _purities;
            public List<double> SilhouetteScores { get; set;} = new List<double>();
            public List<Tuple<int, double, double>> TreeScores { get; set; } = new List<Tuple<int, double, double>>();

            public SilhouetteAssessment(List<HashSet<AbstractNode>> clusters, List<double> purities)
            {
                _clusters = clusters;
                _purities = purities;

            }

            private Int32 levenshtein(String a, String b)
            {

                if (string.IsNullOrEmpty(a))
                {
                    if (!string.IsNullOrEmpty(b))
                    {
                        return b.Length;
                    }
                    return 0;
                }

                if (string.IsNullOrEmpty(b))
                {
                    if (!string.IsNullOrEmpty(a))
                    {
                        return a.Length;
                    }
                    return 0;
                }

                Int32 cost;
                Int32[,] d = new int[a.Length + 1, b.Length + 1];
                Int32 min1;
                Int32 min2;
                Int32 min3;

                for (Int32 i = 0; i <= d.GetUpperBound(0); i += 1)
                {
                    d[i, 0] = i;
                }

                for (Int32 i = 0; i <= d.GetUpperBound(1); i += 1)
                {
                    d[0, i] = i;
                }

                for (Int32 i = 1; i <= d.GetUpperBound(0); i += 1)
                {
                    for (Int32 j = 1; j <= d.GetUpperBound(1); j += 1)
                    {
                        cost = Convert.ToInt32(!(a[i - 1] == b[j - 1]));

                        min1 = d[i - 1, j] + 1;
                        min2 = d[i, j - 1] + 1;
                        min3 = d[i - 1, j - 1] + cost;
                        d[i, j] = Math.Min(Math.Min(min1, min2), min3);
                    }
                }

                return d[d.GetUpperBound(0), d.GetUpperBound(1)];

            }

            private double datumDatumDistance(AbstractNode a, AbstractNode b, bool sameCluster = false)
            {
                if (a is MethodReturnSymbol || b is MethodReturnSymbol)
                {
                    if (sameCluster)
                    {
                        return 0.0;
                    }
                }
                return (double)levenshtein(a.Name, b.Name);

            }

            private List<double> datumClusterDistance(AbstractNode a, HashSet<AbstractNode> cluster, bool sameCluster = false)
            {
                List<double> distance = new List<double>();
                foreach (var datum in cluster)
                {
                    distance.Add(datumDatumDistance(a, datum, sameCluster));
                }
                return distance;
            }

            //compute silhouette scores for every datum in cluster
            private List<double> silhouetteScoresForCluster(HashSet<AbstractNode> cluster)
            {
                List<double> scores = new List<double>();
                foreach (var datum in cluster)
                {
                    /* 
                    Computing a score based on silhouette clustering here: (B(i) - A(i))/max{A(i), B(i)}.
                    A and B are clusters and i is a datum from A. 
                    A(i) is the disimilarity of i with other datum in A. 
                    B(i) is the lowest average disimilarity of i to any other cluster of which i is not a member.
                    */

                    var A_i = datumClusterDistance(datum, cluster, true).Average();
                    double B_i = Double.MinValue;
                    foreach (var cl in _clusters)
                    {
                        var avgDistance = datumClusterDistance(datum, cl).Average();
                        if (B_i < avgDistance)
                        {
                            B_i = avgDistance;
                        }

                    }
                    scores.Add((B_i - A_i) / (new List<double> { A_i, B_i }.Max()));
                }

                return scores;
            }

            public void computeSilhouetteScores()
            {
                SilhouetteScores = _clusters.Select(x => (x.Count>0? silhouetteScoresForCluster(x).Average(): 0.0)).ToList();
            }
        }


        public UDCTree SubTree{ get; set;}
        private SubtypeMiner _miner;
        public int numRelationships {get; set;}
        public List<double> purities {get; set;}
        public double avergagePurity  {get; set;}
        public List<HashSet<AbstractNode>> clusteringResult {  get; set;}
        public List<HashSet<int>> ClusterParents { get; set;}
        public Dictionary<int, List<int>> ancestorMap;
        public List<Tuple<int, double, double>> TreeScores { get; set; } = new List<Tuple<int, double, double>>();

        public SubTreeResults(UDCTree subTree, SubtypeMiner miner)
        {
            SubTree = subTree;
            _miner = miner;
        }

        public void computePurity()
        {
            purities = _miner.ComputePurities(clusteringResult);
            avergagePurity = purities.Count>0? purities.Average(): 0.0;
        }

        public void computeScores() {
            try { 
            var (result, parents) = _miner.InferColors();
            ClusterParents = parents;
            ancestorMap = _miner.AncestorMap;
            clusteringResult =  result;

            computePurity();
            SilhouetteAssessment SAScore = new SilhouetteAssessment(result, purities);
            SAScore.computeSilhouetteScores();
            TreeScores = SAScore.SilhouetteScores.Select((g, i) => new Tuple<int, double, double>(result[i].Count(), purities[i], g)).ToList();
            }
            catch
            {
                Console.WriteLine("Exception in clustering algorithm.");
            }
        }      
    }

    class GodClassResults
    {
        private Func<string, int, string> _pathProcessor;
        private TypeConstraints _typeRelations;
        public List<SubTreeResults>  MiningResults{ get; set;}
        public string ResultsDirectory {get; set; }

        public GodClassResults(Func<string, int, string> pathProcessor, TypeConstraints typeRelations, string results_directory = "results")
        {
            _pathProcessor = pathProcessor;
            _typeRelations = typeRelations;
            MiningResults = new List<SubTreeResults>();
            ResultsDirectory = results_directory + "/" ;

            try
            {
                if (!Directory.Exists(results_directory))
                {
                    Directory.CreateDirectory(results_directory);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                System.Environment.Exit(-1);
            }

            System.IO.DirectoryInfo di = new DirectoryInfo(results_directory);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
        }

        public void startInference(UDCTree UDCHierarchy, int depth = 0)
        {
            SubtypeMiner miner = null;
            var typesAtDepth = UDCHierarchy.GetAllTypesAtDepth(depth);
            foreach (var v in typesAtDepth)
            {
                UDCTree  subTree = new UDCTree(v);
                Console.WriteLine("Running Inference for {0} ...", v.Value.MetadataName);
                var typeNamesOfDescendants = UDCHierarchy.GetTypeNamesOfDescendants(v);
                miner = new SubtypeMiner(new HashSet<string>(typeNamesOfDescendants), _typeRelations, 50);
                SubTreeResults res  = new SubTreeResults(subTree, miner);
                MiningResults.Add(res);

                if(miner.NumRelationships == 0) continue;
                res.computeScores();
                try
                {
                    _typeRelations.ToDot("results/" + v.Value.ToDisplayString() + ".dot", _pathProcessor, res.clusteringResult);
                    writeResultsToDisk( _pathProcessor, res, ResultsDirectory + v.Value.ToDisplayString() + "_result.txt");
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}", e.Message);
                    continue;
                }
            }
        }

        private List<string> randomUniformSample(List<string> alltypes)
        {
            Random rnd = new Random();
            int max_classes = 200;
            if (alltypes.Count() > max_classes)
                alltypes = alltypes.OrderBy(x => rnd.Next()).Take(max_classes).ToList();
            return alltypes;
        }

        public void startConsolidatedInference(UDCTree UDCHierarchy, int depth = 0)
        {
            SubtypeMiner miner = null;
            List <string> alltypes = new List<string>();
            var typesAtDepth = UDCHierarchy.GetAllTypesAtDepth(depth);
            foreach (var v in typesAtDepth)
            {
                List<string> typeNamesOfDescendants = new List<string>();
                Console.WriteLine("Running Inference for {0} ...", v.Value.MetadataName);
                try{ 
                    typeNamesOfDescendants = UDCHierarchy.GetTypeNamesOfDescendants(v);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                alltypes = alltypes.Concat(typeNamesOfDescendants).ToList();
            }
            
            alltypes = randomUniformSample(alltypes);
            UDCTree subTree = new UDCTree(typesAtDepth.First());
            miner = new SubtypeMiner(new HashSet<string>(alltypes), _typeRelations, 50);
            SubTreeResults res = new SubTreeResults(subTree, miner);
            MiningResults.Add(res);

            if (miner.NumRelationships > 0) {
                res.computeScores();
                try
                {
                    //_typeRelations.ToDot(ResultsDirectory + "consolidated.dot", _pathProcessor, res.clusteringResult);
                    writeResultsToDisk(_pathProcessor, res, ResultsDirectory + "consolidated" + "_result.txt");
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}", e.Message);

                }
            }
        }

        public void startTypeSpecificNameFlowInference(UDCTree UDCHierarchy, int depth = 0)
        {
            var typeLocationDict = UDCHierarchy.getUDTLineSpans();
            SubtypeMiner miner = null;
            var typesAtDepth = UDCHierarchy.GetAllTypesAtDepth(depth);
            foreach (var v in UDCHierarchy.getAllTypes())
            {
                if(v.Value.Locations.Length == 0 || !v.Value.Locations.All( x => x.IsInSource)){ 
                    continue; 
                }
                UDCTree subTree = new UDCTree(v);
                Console.WriteLine("Running Inference for {0} ...", v.Value.MetadataName);
                miner = new SubtypeMiner(null, _typeRelations, 50, true, v.Value);
                SubTreeResults res = new SubTreeResults(subTree, miner);

                try { 
                    MiningResults.Add(res);
                    if (miner.NumRelationships == 0) continue;
                    res.computeScores();
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}", e.Message);
                    continue;
                }
                try
                {
                    //_typeRelations.ToDot(ResultsDirectory + v.Value.ToDisplayString() + ".dot", _pathProcessor, res.clusteringResult);
                    writeResultsToDisk(_pathProcessor, res, ResultsDirectory + v.Value.ToDisplayString() + "_result.txt", miner.GetLineSpanOfType(v.Value));
                }
                catch (Exception e)
                {
                    Console.WriteLine("{0}", e.Message);
                    continue;
                }
            }
        }

        public void writeResultsToDisk(
             Func<string, int, string> pathProcessor, SubTreeResults res, string filename = "results/cluster_dict.txt", int linespan = 0)
        {
            /*try
            {
                using (var writer = new StreamWriter(filename))
                {
                    int i = 0;
                    foreach (var color in res.clusteringResult)
                    {
                        writer.WriteLine($"===============Cluster {i + 1}: Parents: [{string.Join(", ", res.ClusterParents[i].Select(k => k + 1))}]===================");
                        i++;
                        foreach (AbstractNode node in color)
                        {
                            string path;
                            if (node.Location != null)
                            {
                                if (node.Location.SourceTree != null)
                                {
                                    path = pathProcessor(node.Location.SourceTree.FilePath,
                                        node.Location.GetLineSpan().StartLinePosition.Line);
                                }
                                else
                                {
                                    path = node.Location.ToString();
                                }
                            }
                            else
                            {
                                path = "unknown path";
                            }
                            //writer.WriteLine($"{node} at {path}");
                            writer.WriteLine($"{node}");
                        }
                    }
                }
                //Console.WriteLine("Average purity: {0:0.000}", avergagePurity);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.Message);
            }*/

            try
            {
                Dictionary<string, Object> clusDict = new Dictionary<string, Object>();
                using (var writer = new StreamWriter(filename))
                {
                    int i = 0;
                    foreach (var color in res.clusteringResult)
                    {
                        i++;
                        var d = new List<Dictionary<string, string>>();
                        foreach (AbstractNode node in color)
                        {
                            var v = new Dictionary<string, string>();
                            v.Add("Type", node.Type);
                            v.Add("Name", node.Name);
                            v.Add("Location", node.Location.GetMappedLineSpan().Path);
                            v.Add("Linespan", node.Location.GetMappedLineSpan().StartLinePosition.Line.ToString());
                            d.Add(v);
                        }
                        try
                        {
                            clusDict.Add(i.ToString(), d);
                        }
                        catch
                        {
                            clusDict[i.ToString()] = d;
                        }
                        clusDict["linespan"] = linespan;
                    }
                    writer.WriteLine( JsonConvert.SerializeObject(clusDict, Formatting.Indented));
                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.Message);
            }
            /*
            var typename = res.SubTree.UDCTreeRoots.First().Value.Name;
            Dictionary < string, List< Tuple < int, double, double>>> scores = new Dictionary<string, List<Tuple<int, double, double>>>();
            scores.Add("clusters", res.TreeScores);
            // write scores to a file
            string scores_str = JsonConvert.SerializeObject(scores, Formatting.Indented);
            // Write the string to a file.
            var file = new System.IO.StreamWriter("results/"+ typename +"_scores.txt");
            file.WriteLine(scores_str);
            file.Close();
            */
        }
    }
}
