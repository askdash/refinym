using System.Collections.Generic;
using System.Linq;

namespace SubtypeInduction.TypeSystemRels
{

    class TCNode
    {
        public bool Completed { get; set; } = false;
        public List<TCNode> Ancestors;
        public List<TCNode> Parents;

        public TCNode(int id)
        {
            Ancestors = new List<TCNode>();
            Parents = new List<TCNode>();
            ID = id;
        }

        public int ID { get; }
    }

    class RewriterFromJson
    {
        public Dictionary<int, List<int>> AncestorMap;
        public List<HashSet<AbstractNode>> Clusters { get; set; }
        public List<HashSet<int>> Parents { get; set; }
        private readonly List<TCNode> closure = new List<TCNode>();

        public RewriterFromJson(string filename, string repositoryPath, TypeConstraints recollectedConstraints)
        {
            ClusteringSerializerUtil csu = new ClusteringSerializerUtil();
            (Clusters, Parents) = csu.Deserialize(filename, repositoryPath, recollectedConstraints);
            AncestorMap = new Dictionary<int, List<int>>();
            SetAncestorMap();
        }

        private List<TCNode> ComputeClosure(TCNode n)
        {
            if (n.Completed) return n.Ancestors;
            List<TCNode> newAncestors = new List<TCNode>();

            newAncestors.AddRange(n.Parents);

            foreach (var v in n.Parents)
            {
                if (v.Completed)
                {
                    newAncestors.AddRange(v.Ancestors);
                }
                else
                {
                    newAncestors.AddRange(ComputeClosure(v));
                }
            }

            foreach (var a in newAncestors)
            {
                if (!n.Ancestors.Contains(a))
                {
                    n.Ancestors.Add(a);
                }
            }

            n.Completed = true;
            return n.Ancestors;
        }

        private void SetAncestorMap()
        {
            int id = 0;
            List<TCNode> nodelist = new List<TCNode>();
            foreach (var hs in Parents)
            {
                nodelist.Add(new TCNode(id++));
            }

            id = 0;
            foreach (var hs in Parents)
            {
                nodelist[id++].Parents.AddRange(hs.Select(x => nodelist[x]));
            }

            foreach (var c in nodelist)
            {
                if (!c.Completed)
                {
                    ComputeClosure(c);
                }
                var ancestorList = c.Ancestors.Select(x => nodelist.IndexOf(x)).ToList();
                AncestorMap.Add(c.ID, ancestorList);
            }
        }

    }
}