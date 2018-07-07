using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;

namespace SubtypeInduction.TypeSystemRels
{
    class ClusteringSerializerUtil
    {
        static string LocationToString(string repositoryPath, AbstractNode node)
        {
            try
            {
                Debug.Assert(node.Location.SourceTree.FilePath.StartsWith(repositoryPath));
                var lineSpan = node.Location.GetMappedLineSpan();
                var s = $"{node.Location.SourceTree.FilePath.Substring(repositoryPath.Length)}:{lineSpan.StartLinePosition}->{lineSpan.EndLinePosition}:{node}";
                return s;
            }
            catch (Exception)
            {
                System.Console.WriteLine(node.Location.ToString(), repositoryPath);
                return null;
            }
        }

        public static void SerializeClustering(string filename, string repositoryPath, List<HashSet<AbstractNode>> clusters, List<HashSet<int>> parents)
        {
            var ser = new SerializableCluster
            {
                Clusters = clusters.Select(c => new HashSet<string>(c.Select(e => LocationToString(repositoryPath, e)))).ToList(),
                Parents = parents
            };
            File.WriteAllText(filename, JsonConvert.SerializeObject(ser));
        }

        public AbstractNode getNodeLocation(Dictionary<string, AbstractNode> nodeLocationToString, string key)
        {
            try
            {
                return nodeLocationToString[key];
            }
            catch
            {
                return null;
            }
        }

        public (List<HashSet<AbstractNode>> Clusters, List<HashSet<int>> Parents) Deserialize(string filename, string repositoryPath, TypeConstraints recollectedConstraints)
        {
            var allNodes = recollectedConstraints.AllRelationships.Select(kv => kv.Key).Concat(recollectedConstraints.AllRelationships.SelectMany(kv => kv.Value));

            var allNodesFiltered = allNodes.GroupBy(x => LocationToString(repositoryPath, x)).Select(grp => grp.First());
            var nodeLocationToString = allNodesFiltered.ToDictionary(n => LocationToString(repositoryPath, n), n => n);
            // Load file
            var deserialized = JsonConvert.DeserializeObject<SerializableCluster>(File.ReadAllText(filename));
            // re-map
            var clusters = deserialized.Clusters.Select(c => new HashSet<AbstractNode>(c.Select(l => getNodeLocation(nodeLocationToString, l)/*nodeLocationToString[l]*/))).ToList();

            return (clusters, deserialized.Parents);
        }

        public struct SerializableCluster
        {
            public List<HashSet<string>> Clusters;
            public List<HashSet<int>> Parents;
        }
    }
}
