using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SubtypeInduction.TypeSystemRels
{
    public class TypeConstraints
    {
        public TypeConstraints(Func<string, int, string> pathProcessor)
        {
            _pathProcessor = pathProcessor;
        }

        public readonly Dictionary<AbstractNode, HashSet<AbstractNode>> AllRelationships =
            new Dictionary<AbstractNode, HashSet<AbstractNode>>();

        private readonly Func<string, int, string> _pathProcessor;

        public void AddFromCompilation(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var collector = new FileTypeRelationCollector(semanticModel, AllRelationships);
                collector.Visit(tree.GetRoot());
            }
        }

        public void RemoveSelfLinks()
        {
            var toRemove = AllRelationships.Where(kv => kv.Value.Contains(kv.Key)).Select(kv => kv.Key).ToArray();
            foreach (var node in toRemove)
            {
                AllRelationships[node].Remove(node);
            }
        }

        public void ToDot(string filename, Func<string, int, string> pathProcessor, List<HashSet<AbstractNode>> grouping)
        {
            using (var f = new StreamWriter(filename))
            {
                f.WriteLine("digraph \"extractedGraph\"{"); ;

                // Write all nodes
                var allNodes = new HashSet<AbstractNode>(AllRelationships.Select(n => n.Key).Concat(AllRelationships.SelectMany(n => n.Value)));
                var nodeIds = new Dictionary<AbstractNode, int>();
                int nextId = 0;
                int groupI = 0;
                foreach (var group in grouping)
                {
                    groupI++;
                    f.WriteLine("subgraph cluster_" + groupI.ToString() + " {" + "style=filled; color = lightgrey; node[style = filled, shape=box; color = white]; ");
                    foreach (var node in group)
                    {
                        var removed = allNodes.Remove(node);
                        Debug.Assert(removed);
                        string path = "Unk/External";
                        if (node.Location != null && node.Location.SourceTree != null)
                        {
                            path = pathProcessor(node.Location.SourceTree.FilePath, node.Location.GetLineSpan().StartLinePosition.Line);
                        }
                        //f.WriteLine("n{0} [label=\"{1}\"];  //{2}", nextId, DotEscape(node.ToString()), path);
                        f.WriteLine("n{0} [label=\"{1}\"]; ", nextId, /*DotEscape(node.ToDotString())*/ node.ToDotString());
                        nodeIds[node] = nextId;
                        nextId++;
                    }
                    f.WriteLine("} //subgraph cluster_" + groupI.ToString());
                }


                foreach (var parentChildrenPair in AllRelationships)
                {
                    var parent = parentChildrenPair.Key;
                    foreach (var child in parentChildrenPair.Value)
                    {
                        if (nodeIds.ContainsKey(parent) && nodeIds.ContainsKey(child))
                        {
                            f.WriteLine("n{0}->n{1};", nodeIds[parent], nodeIds[child]);
                        }
                    }
                }

                f.WriteLine("}");
            }
        }

        public void ToJson(string filename)
        {
            HashSet<AbstractNode> allNodes = new HashSet<AbstractNode>();
            allNodes.UnionWith(AllRelationships.Keys);
            allNodes.UnionWith(AllRelationships.Values.SelectMany(n => n));
            Dictionary<AbstractNode, int> nodeToId = new Dictionary<AbstractNode, int>();

            List<Dictionary<string, string>> nodeJson = new List<Dictionary<string, string>>();
            foreach (var node in allNodes)
            {
                nodeToId.Add(node, nodeToId.Count);
                nodeJson.Add(NodeAsJsonInfo(node));
            }

            // Add relations
            List<List<int>> relations = Enumerable.Range(0, nodeToId.Count).Select<int, List<int>>(i => null).ToList();
            foreach (var relationship in AllRelationships)
            {
                int parentId = nodeToId[relationship.Key];
                relations[parentId] = relationship.Value.Select(n => nodeToId[n]).ToList();
            }

            using (var fileStream = File.Create(filename))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Compress, false))
            using (var textStream = new StreamWriter(gzipStream, Encoding.UTF8))
            {
                var serializer = new JsonSerializer { NullValueHandling = NullValueHandling.Ignore };
                serializer.Serialize(textStream, new Dictionary<string, object>() { { "nodes", nodeJson }, { "relations", relations } });
            }
        }

        private Dictionary<string, string> NodeAsJsonInfo(AbstractNode node)
        {
            var info = new Dictionary<string, string>();
            if (node.Location != null && node.Location.IsInSource)
            {
                info["Location"] = _pathProcessor.Invoke(node.Location.SourceTree.FilePath,
                    node.Location.GetLineSpan().StartLinePosition.Line);
            }
            else
            {
                info["Location"] = ((object)node.Location ?? "implicit").ToString();
            }

            if (node is LiteralSymbol)
            {
                info["value"] = node.ToString();
                info["kind"] = "const";
            }
            else if (node is MethodReturnSymbol)
            {
                info["name"] = node.Name;
                info["kind"] = "methodReturn";
                info["type"] = ((MethodReturnSymbol)node).Type;
                if (((MethodReturnSymbol)node).IsConstructor)
                {
                    info["symbolKind"] = "constructor";
                }
                else
                {
                    info["symbolKind"] = "method";
                }
            }
            else
            {
                info["name"] = node.Name;
                var varNode = node as VariableSymbol;
                info["kind"] = "variable";
                info["type"] = varNode.Type;
                info["symbolKind"] = varNode.Symbol.Kind.ToString();
            }

            return info;
        }

        private static string DotEscape(string input)
        {
            return input.Replace("\"", "''").Replace('\r', ' ').Replace('\n', ' ').Replace("\\", "\\\\").Replace('^', ' ');
        }
    }

    public abstract class AbstractNode
    {
        public abstract bool IsSymbol { get; }
        public abstract bool IsLiteral { get; }
        public abstract string Name { get; }
        public abstract Location Location { get; }
        public abstract string Type { get; }
        public abstract string ToDotString();
    }

    internal class VariableSymbol : AbstractNode
    {
        public VariableSymbol(ISymbol symbol)
        {
            Symbol = symbol.OriginalDefinition;
        }

        public ISymbol Symbol { get; }

        public override bool IsSymbol => true;
        public override bool IsLiteral => false;
        public override string Name => /*Symbol.ContainingNamespace.Name + "." +*/ Symbol.Name;
        public override Location Location
        {
            get
            {
                if (Symbol.IsImplicitlyDeclared) return null;
                return Symbol.Locations[0];
            }
        }

        public override string Type
        {
            get
            {
                if (Symbol is ILocalSymbol)
                {
                    return ((ILocalSymbol)Symbol).Type.ToDisplayString();
                }
                else if (Symbol is IPropertySymbol)
                {
                    return ((IPropertySymbol)Symbol).Type.ToDisplayString();
                }
                else if (Symbol is IParameterSymbol)
                {
                    return ((IParameterSymbol)Symbol).Type.ToDisplayString();
                }
                else if (Symbol is IFieldSymbol)
                {
                    return ((IFieldSymbol)Symbol).Type.ToDisplayString();
                }
                return "Unknown";
            }
        }

        public override int GetHashCode()
        {
            return Symbol.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var o = obj as VariableSymbol;
            if (o == null) return false;
            return Location.SourceTree.FilePath == o.Location.SourceTree.FilePath &&
                Location.GetLineSpan().StartLinePosition.Line == o.Location.GetLineSpan().StartLinePosition.Line &&
                Symbol.Name == o.Name;
        }

        public override string ToString()
        {
            return Name + " : " + Type;
        }

        public override string ToDotString()
        {
            return Name + "\n<B>" + Type + "</B>";
        }
    }

    class MethodReturnSymbol : AbstractNode
    {
        public MethodReturnSymbol(IMethodSymbol symbol)
        {
            Symbol = symbol.OriginalDefinition;
        }

        public IMethodSymbol Symbol { get; }

        public override bool IsSymbol => true;
        public override bool IsLiteral => false;

        public bool IsConstructor => Symbol.Name == ".ctor";

        public override string Name
        {
            get
            {
                if (Symbol.Name == ".ctor")
                {
                    return /*Symbol.ContainingNamespace.Name + "." +*/ Symbol.ContainingType.Name;
                }
                return /*Symbol.ContainingNamespace.Name + "." +*/ Symbol.Name;
            }
        }

        public override Location Location
        {
            get
            {
                if (Symbol.IsImplicitlyDeclared) return null;
                return Symbol.Locations[0];
            }
        }
        public override string Type => Symbol.ReturnType?.ToDisplayString();

        public override int GetHashCode()
        {
            return Symbol.Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var o = obj as MethodReturnSymbol;
            if (o == null) return false;
            return Location.SourceTree.FilePath == o.Location.SourceTree.FilePath &&
                Location.GetLineSpan().StartLinePosition.Line == o.Location.GetLineSpan().StartLinePosition.Line &&
                Symbol.Name == o.Name;
        }

        public override string ToString()
        {
            return Symbol.ToDisplayString() + " : " + Type;
        }

        public override string ToDotString()
        {
            return "*" + Name + "\n<B>" + Type + "</B>";
        }
    }

    class LiteralSymbol : AbstractNode
    {
        public LiteralSymbol(object constant, Location location)
        {
            Constant = constant;
            Location = location;
        }

        public object Constant { get; }

        public override bool IsSymbol => false;
        public override bool IsLiteral => true;
        public override string Name { get { return ""; } }
        public override Location Location { get; }
        public override string Type
        {
            get
            {
                if (Constant == null)
                    return "null";
                switch (Constant)
                {
                    case string s:
                        return "string";
                    case int i:
                        return "int";
                    default:
                        return Constant.GetType().ToString();
                }
            }
        }

        public override int GetHashCode()
        {
            if (Constant == null) return 0;
            return Constant.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var o = obj as LiteralSymbol;
            if (o == null) return false;
            return Object.Equals(o.Constant, Constant);
        }

        public override string ToString()
        {
            if (Constant == null) return "const:null";
            return "const:" + Constant.ToString() + " : " + Type;
        }

        public override string ToDotString()
        {
            if (Constant == null) return "const\n<B>null</B>";
            return "const:" + Constant.ToString() + "\n<B>" + Type + "</B>";
        }
    }

    class FileTypeRelationCollector : CSharpSyntaxWalker
    {

        private readonly Dictionary<AbstractNode, HashSet<AbstractNode>> SubtypingRelationships;

        private readonly SemanticModel _semanticModel;

        private readonly bool _includeExternalSymbols;

        public FileTypeRelationCollector(SemanticModel model, Dictionary<AbstractNode, HashSet<AbstractNode>> relationships,
            bool includeExternalSymbols = false)
        {
            _semanticModel = model;
            SubtypingRelationships = relationships;
            _includeExternalSymbols = includeExternalSymbols;
        }

        private void AddSubtypingRelation(AbstractNode moreGeneralType, AbstractNode moreSpecificType)
        {
            if (!SubtypingRelationships.TryGetValue(moreGeneralType, out HashSet<AbstractNode> children))
            {
                children = new HashSet<AbstractNode>();
                SubtypingRelationships.Add(moreGeneralType, children);
            }
            children.Add(moreSpecificType);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            var invocationSymbol = (IMethodSymbol)_semanticModel.GetSymbolInfo(node).Symbol;
            if (invocationSymbol == null) return;
            if (!IsUsedSymbol(invocationSymbol)) return;

            invocationSymbol = invocationSymbol.OriginalDefinition;
            AddAllMethods(invocationSymbol);

            foreach (var arg in node.ArgumentList.Arguments)
            {
                var paramSymbol = DetermineParameter(node.ArgumentList, arg, invocationSymbol);

                var argSymbol = GetNodeSymbol(arg.Expression);
                if (argSymbol != null)
                {
                    AddSubtypingRelation(new VariableSymbol(paramSymbol), argSymbol);
                }
            }
        }

        private void AddAllMethods(IMethodSymbol methodSymbol)
        {
            if (!IsUsedSymbol(methodSymbol)) return;
            var methodIfaces = methodSymbol.ContainingType.AllInterfaces.SelectMany(iface => iface.GetMembers().OfType<IMethodSymbol>())
                .Where(method => methodSymbol.Equals(methodSymbol.ContainingType.FindImplementationForInterfaceMember(method))).ToArray();
            foreach (var iface in methodIfaces)
            {
                if (!IsUsedSymbol(iface)) continue;
                for (int i = 0; i < iface.Parameters.Length; i++)
                {
                    AddSubtypingRelation(new VariableSymbol(methodSymbol.Parameters[i]), new VariableSymbol(iface.Parameters[i]));
                }
                AddSubtypingRelation(new MethodReturnSymbol(iface), new MethodReturnSymbol(methodSymbol));
                AddAllMethods(iface);
            }

            //TODO: Add all its erasures of generics?

            var overriden = methodSymbol.OverriddenMethod;
            if (overriden != null)
            {
                if (!IsUsedSymbol(overriden)) return;
                for (int i = 0; i < overriden.Parameters.Length; i++)
                {
                    AddSubtypingRelation(new VariableSymbol(methodSymbol.Parameters[i]), new VariableSymbol(overriden.Parameters[i]));
                }
                AddSubtypingRelation(new MethodReturnSymbol(overriden), new MethodReturnSymbol(methodSymbol));
                AddAllMethods(overriden);
            }
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            base.VisitAssignmentExpression(node);

            var rightSymbol = _semanticModel.GetSymbolInfo(node.Right).Symbol;

            if (node.Left is ElementAccessExpressionSyntax)
            {
                // TODO 
            }
            else
            {
                var leftSymbol = _semanticModel.GetSymbolInfo(node.Left).Symbol;
                if (!IsUsedSymbol(leftSymbol) || !IsUsedSymbol(rightSymbol)) return;

                var constValue = _semanticModel.GetConstantValue(node.Right);
                if (rightSymbol != null && leftSymbol != null) // rightSymbol may be null if we have a boolean expression
                {
                    AbstractNode rightSymbolNode;
                    if (rightSymbol is IMethodSymbol)
                    {
                        rightSymbolNode = new MethodReturnSymbol(rightSymbol as IMethodSymbol);
                    }
                    else
                    {
                        rightSymbolNode = new VariableSymbol(rightSymbol);
                    }
                    
                    AddSubtypingRelation(new VariableSymbol(leftSymbol), rightSymbolNode);
                }
                else if (constValue.HasValue && leftSymbol != null)
                {
                    AddSubtypingRelation(new VariableSymbol(leftSymbol), new LiteralSymbol(constValue.Value, node.GetLocation()));
                }
            }
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            base.VisitVariableDeclarator(node);
            var declaredSymbol = _semanticModel.GetDeclaredSymbol(node);
            if (node.Initializer == null) return;
            var assignedFrom = _semanticModel.GetSymbolInfo(node.Initializer.Value).Symbol;
            if (!IsUsedSymbol(declaredSymbol)) return;
            
            if (IsUsedSymbol(assignedFrom))
            {
                if (assignedFrom is IMethodSymbol methodSymbol)
                {
                    AddSubtypingRelation(new VariableSymbol(declaredSymbol), new MethodReturnSymbol(methodSymbol));
                }
                else
                {
                    AddSubtypingRelation(new VariableSymbol(declaredSymbol), new VariableSymbol(assignedFrom));
                }
            } else if (_semanticModel.GetConstantValue(node.Initializer.Value).HasValue)
            {
                AddSubtypingRelation(new VariableSymbol(declaredSymbol),
                    new LiteralSymbol(_semanticModel.GetConstantValue(node.Initializer.Value).Value, node.Initializer.Value.GetLocation()));
            }
        }

        private AbstractNode GetNodeSymbol(SyntaxNode node)
        {
            if (node is CastExpressionSyntax)
            {
                var cast = node as CastExpressionSyntax;
                node = cast.Expression;
            }

            var constant = _semanticModel.GetConstantValue(node);
            if (!(node is IdentifierNameSyntax) && constant.HasValue)
            {
                return new LiteralSymbol(constant.Value, node.GetLocation());
            }

            var symbol = _semanticModel.GetSymbolInfo(node).Symbol ?? _semanticModel.GetDeclaredSymbol(node);
            if (symbol == null || !IsUsedSymbol(symbol))
            {
                return null;
            }
            if (symbol is IMethodSymbol)
            {
                return new MethodReturnSymbol(symbol as IMethodSymbol);
            }
            return new VariableSymbol(symbol);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var methodSymbol = _semanticModel.GetDeclaredSymbol(node);
            AddAllMethods(methodSymbol);
            base.VisitMethodDeclaration(node);
        }

        public override void VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (node.Expression == null) return;
            var nodeSymbol = GetNodeSymbol(node.Expression);

            if (nodeSymbol != null)
            {
                SyntaxNode returningPoint = node;
                while (returningPoint != null &&
                       !((returningPoint is BaseMethodDeclarationSyntax) ||
                         (returningPoint is AccessorDeclarationSyntax) ||
                         (returningPoint is AnonymousFunctionExpressionSyntax)))
                {
                    returningPoint = returningPoint.Parent;
                }

                AbstractNode returningNode;
                if (returningPoint is BaseMethodDeclarationSyntax || returningPoint is AnonymousFunctionExpressionSyntax)
                {
                    var methodSymbol =
                        (_semanticModel.GetDeclaredSymbol(returningPoint) ??
                         _semanticModel.GetSymbolInfo(returningPoint).Symbol) as IMethodSymbol;
                    returningNode = new MethodReturnSymbol(methodSymbol);
                }
                else if (returningPoint is AccessorDeclarationSyntax)
                {
                    var propertySymbol = _semanticModel.GetDeclaredSymbol(returningPoint.Parent.Parent);
                    returningNode = new VariableSymbol(propertySymbol);
                }
                else
                {
                    throw new Exception("Never Happens");
                }
                AddSubtypingRelation(returningNode, nodeSymbol);
            }
            base.VisitReturnStatement(node);
        }

        private bool IsUsedSymbol(ISymbol symbol)
        {
            if (symbol == null) return false;
            if (_includeExternalSymbols) return true;
            return !symbol.IsImplicitlyDeclared && !symbol.Locations[0].IsInMetadata;
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            base.VisitBinaryExpression(node);

            var invocationSymbol = (IMethodSymbol)_semanticModel.GetSymbolInfo(node).Symbol;
            if (invocationSymbol != null)
            {
                if (!IsUsedSymbol(invocationSymbol)) return;

                if (invocationSymbol.Parameters.Length > 1)
                {
                    var rightSymbol = GetNodeSymbol(node.Right);
                    if (rightSymbol != null) AddSubtypingRelation(new VariableSymbol(invocationSymbol.Parameters[1]), rightSymbol);
                }

                var leftSymbol = GetNodeSymbol(node.Left);
                if (leftSymbol != null) AddSubtypingRelation(new VariableSymbol(invocationSymbol.Parameters[0]), leftSymbol);
            }
        }
        
        /// <summary>
        /// Copied from Roslyn source code. Determines the parameter for a given argument
        /// </summary>
        /// <param name="argumentList"></param>
        /// <param name="argument"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public IParameterSymbol DetermineParameter(BaseArgumentListSyntax argumentList, ArgumentSyntax argument, IMethodSymbol symbol)
        {
            var parameters = symbol.Parameters;

            // Handle named argument
            if (argument.NameColon != null && !argument.NameColon.IsMissing)
            {
                var name = argument.NameColon.Name.Identifier.ValueText;
                return parameters.FirstOrDefault(p => p.Name == name);
            }

            // Handle positional argument
            var index = argumentList.Arguments.IndexOf(argument);
            if (index < 0)
            {
                return null;
            }

            if (index < parameters.Length)
            {
                return parameters[index];
            }

            // Handle Params
            var lastParameter = parameters.LastOrDefault();
            if (lastParameter == null)
            {
                return null;
            }

            if (lastParameter.IsParams)
            {
                return lastParameter;
            }

            return null;
        }
    }
}
