using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Linq;

//UDC stands for user-defined classes. We ignore templated class defs for simplicity.

namespace AbstractTreeNamespace {

    public class TreeNodeHasMultipleParentsException : Exception
    {
        public TreeNodeHasMultipleParentsException(string message) : base(message)
        {
        }
    }
    public class AbstractTree<T>
    {
        private readonly T _value;
        private readonly List<AbstractTree<T>> _children;
        private AbstractTree<T> _parent;

        public AbstractTree(T value)
        {
            _value = value;
            _parent  =  null;
            _children = new List<AbstractTree<T>>();
        }

        public int Depth { get; set;}

        public AbstractTree<T> this[int i]
        {
            get { return _children[i]; }
        }

        public AbstractTree<T> Parent { 
            get { return _parent;} 
            set { 
                if (_parent!=null) { 
                    throw (new TreeNodeHasMultipleParentsException
                        ("Tree node cannot have multiple parents."));
                }
                _parent = value;
            } 
        }

        public T Value { get { return _value; } }

        public List<AbstractTree<T>> Children
        {
            get { return _children; }
        }

        public AbstractTree<T> AddChild(T value)
        {
            var node = new AbstractTree<T>(value) { Parent = this };
            _children.Add(node);
            return node;
        }
    }
}

namespace UDCTreeNamespace {

    using UDCNode = AbstractTreeNamespace.AbstractTree<ITypeSymbol>;

    public class UDCTree
    {

        public class UDCComparer : IComparer<UDCNode>
        {
            public int Compare(UDCNode x, UDCNode y)
            {
                return (x.Value.ContainingNamespace.Name + x.Value.Name)
                    .CompareTo(y.Value.ContainingNamespace.Name + y.Value.Name);
            }

        }

        public List<UDCNode> UDCTreeRoots { get; set;} = new List<UDCNode>();
        private List<UDCNode> allUDCs = new List<UDCNode>();
        private readonly UDCComparer nodeComparer = new UDCComparer();
        private static Random randomSeed = new Random();

        public UDCTree()
        {

        }
        public UDCTree(UDCNode root)
        {
            UDCTreeRoots.Add(root);
            allUDCs.Add(root);
        }

        public UDCTree(ITypeSymbol t)
        {
            UDCNode type = new UDCNode(t);
            allUDCs.Add(type);
            UDCTreeRoots.Add(type);
        }
        private void UpdateDepthOfNodes(List<UDCNode> nodes, int depth=0 )
        {
            foreach( var v in nodes)
            {
                v.Depth = depth;
                UpdateDepthOfNodes(v.Children, depth + 1);
            }
        }
   
        private void RecursiveNamespaceWalker(INamespaceSymbol ns)
        {
            var constituentTypes = ns.GetTypeMembers();
            var constituentNamespaces = ns.GetNamespaceMembers();
            foreach (var type in constituentTypes)
            {
                
                var subType = type;
                var superType = type.BaseType;

                //Ignore generics and abstract types.
                var  subtypeBlacklist  =  subType.IsGenericType  ||  subType.IsAbstract;
                var  superTypeBlacklist = superType != null && (superType.IsGenericType  ||
                                                                superType.IsAbstract);
                if (subtypeBlacklist || superTypeBlacklist /*|| superType.Name.StartsWith("System")*/)
                {
                    continue;
                }
                
                if(!(subType.Locations.All(x => x.IsInSource))) continue;
                if(!(superType.Locations.All(x => x.IsInSource))) superType = null;

                UDCNode subNode = new UDCNode(subType);

                int subIndex = allUDCs.BinarySearch(subNode, nodeComparer);
                if (subIndex < 0)
                {
                    subIndex = ~subIndex;
                    allUDCs.Insert(subIndex, subNode);
                }
                else
                {
                    subNode = allUDCs[subIndex];
                }

                if (superType != null) { 
                    UDCNode superNode = new UDCNode(superType);
                    var v  =  superType.GetType();
                    int superIndex = allUDCs.BinarySearch(superNode, nodeComparer);
                    if (superIndex < 0)
                    {
                        superIndex =  ~superIndex;
                        allUDCs.Insert(superIndex, superNode);
                    }
                    else
                    {
                        superNode = allUDCs[superIndex];
                    }
               
                    int subIndexInChildren = superNode.Children.BinarySearch(subNode, nodeComparer);
                    if (subIndexInChildren < 0)
                    {
                        subIndexInChildren = ~subIndexInChildren;
                        superNode.Children.Insert(subIndexInChildren, subNode);
                        try {
                            subNode.Parent =  superNode;
                        }
                        catch (AbstractTreeNamespace.TreeNodeHasMultipleParentsException e)
                        {
                            Console.WriteLine("TreeNodeHasMultipleParentsException: {0}",
                                e.Message);
                        }
                    }
                }
            }

            var types = new List<INamedTypeSymbol>();
            foreach (var nsIter in constituentNamespaces)
            {
                RecursiveNamespaceWalker(nsIter);
            }
        }

        public void UDCTreeToDot(string assemblyName)
        {
            StreamWriter f = new StreamWriter(assemblyName  + ".type_hierarchy.dot");
            f.WriteLine("digraph \"typeHierarchy\"{");
            foreach (var node in allUDCs)
            {
                foreach (var child  in node.Children) { 
                    f.WriteLine ("\"{0}\"->\"{1}\";", node.Value, child.Value);
                }
            }
            f.WriteLine("}");
            f.Close();
        }

        public List<UDCNode> GetAllDescendantsOfType(UDCNode n)
        {
            List<UDCNode> descendants = n.Children.Select(x => GetAllDescendantsOfType(x)).SelectMany(x => x).ToList();
            descendants.Add(n);
            return descendants;
        }
        
        public List<UDCNode> GetAllTypesAtDepth(int depth) => allUDCs.Where(x=>x.Depth==depth).ToList();
        
        public List<UDCNode> GetRandomSubtree()
        {
            UDCNode randomNode = UDCTreeRoots[randomSeed.Next(allUDCs.Count)];
            return GetAllDescendantsOfType(randomNode);
        }

        public List<String> GetTypeNamesOfDescendants(UDCNode n)
        {
            List<string> typesOfChildren = new List<string>(); 
            if(n.Children != null) { 
                List<List<string>> childrenNames = n.Children.Where(x => !x.Equals(n)).Select(x => GetTypeNamesOfDescendants(x)).ToList();
                typesOfChildren = childrenNames.SelectMany(x => x).ToList();
            }
            typesOfChildren.Add(n.Value.ToString());
            return typesOfChildren;
        }

        public Dictionary<ITypeSymbol, List<Location>> getUDTLineSpans()
        {
            Dictionary<ITypeSymbol, List<Location>> linespans = new Dictionary<ITypeSymbol, List<Location>>();
            foreach (var t in allUDCs)
            {
                linespans[t.Value] = t.Value.Locations.ToList();
            }
            return linespans;
        }

        public List<UDCNode> getAllTypes()
        {
            return allUDCs;
        }

        public void ParseTypesInCompilation(CSharpCompilation c)
        {
           
            foreach (var nsIter in c.GlobalNamespace.GetNamespaceMembers())
            {
                //Ignore the systems namespace, otherwise we get huge hierarchies
                if(nsIter.Name.StartsWith("System")) continue;
                RecursiveNamespaceWalker(nsIter);
            }

            UDCTreeRoots = allUDCs.Where(x => x.Parent == null).ToList();
            UpdateDepthOfNodes(UDCTreeRoots);            
        }
    }

}
