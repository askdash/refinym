using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.IO;

namespace SubtypeInduction.TypeSystemRels
{

    class Rewriter
    {
        private readonly List<CSharpCompilation> _compilations;
        private readonly List<HashSet<AbstractNode>> _clusters;
        private RewriteWalker rewriteWalker;
        private TypeCaster typeCaster;
        public List<string> classDeclarations;
        private readonly Dictionary<int, List<int>> _ancestorMap;
        private readonly string _typename;
        public Dictionary<string, List<Tuple<Diagnostic, string>>> ErrorHistogram { get; set; }
            = new Dictionary<string, List<Tuple<Diagnostic, string>>>();


        public Rewriter(List<CSharpCompilation> compilations, List<HashSet<AbstractNode>> clusters, Dictionary<int, List<int>> ancestorMap, string typename)
        {
            _compilations = compilations;
            _clusters = clusters;
            _ancestorMap = ancestorMap;
            _typename = typename;
            classDeclarations = new List<string>();
            typeCaster = new TypeCaster(_typename);
            rewriteWalker = new RewriteWalker(_typename);

            rewriteWalker.InitRewrites(_clusters);
            int clusterID = 0;
            foreach (var colors in _clusters)
            {
                classDeclarations.Add(GetClusterAsClassDecl(clusterID, ancestorMap[clusterID++]));
            }
        }

        private string GetClusterAsClassDecl(int clusterID, List<int> ancestorIDs)
        {
            string className = "Cluster" + clusterID.ToString();
            var preface = string.Format(@"
public class {0}
{{   
    public {0}({1} d) {{ val = d; }}
    public {1} val;
    public static implicit operator {0}(ref {0} d){{return d.Value;}}
    public static implicit operator ref {0}({0} d){{return new {0}(d.val);}}
    public static implicit operator {1}({0} d){{return d.val;}}
    public static implicit operator {0}({1} d){{return new {0}(d);}}", className, _typename);
            foreach (var id in ancestorIDs /*Enumerable.Range(0, _ancestorMap.Count).Except(new List<int>{clusterID })*/)
            {
                var ancestorName = "Cluster" + id.ToString();
                preface += string.Format(@"
    public static implicit operator {0}({1} d){{return new {0}(d.val);}}", ancestorName, className);
            }

            var postface = @"}";
            return preface + postface;
        }

        public void RewriteTypes()
        {
            string namespaceDescription = @"namespace BespokeLattice {\n" + string.Join("\n", classDeclarations) + "\n};\n";

            foreach (var compilation in _compilations)
            {
                var typeLatticeTree = CSharpSyntaxTree.ParseText(namespaceDescription);
                typeLatticeTree = typeLatticeTree.GetRoot().SyntaxTree.WithRootAndOptions(typeLatticeTree.GetRoot(), compilation.SyntaxTrees[0].Options);
                var modifiedCompilation = compilation.AddSyntaxTrees(new SyntaxTree[] { typeLatticeTree });
                var m = modifiedCompilation.GetSemanticModel(typeLatticeTree);

                foreach (var tree in modifiedCompilation.SyntaxTrees)
                {
                    var newSourceFile = tree.FilePath + "_refinym_modified.cs";

                    if (tree.IsEquivalentTo(typeLatticeTree)) continue;

                    var finalTree = tree;

                    rewriteWalker = new RewriteWalker(_typename)
                    {
                        SemanticModel = modifiedCompilation.GetSemanticModel(tree),
                        Rewrote = false
                    };
                    rewriteWalker.InitRewrites(_clusters);
                    var rewriteResult = rewriteWalker.Visit(tree.GetRoot());
                    SyntaxTree fixedFinalTree = finalTree;

                    if (rewriteWalker.Rewrote)
                    {

                        typeCaster = new TypeCaster(_typename);
                        var fixedRewriteResultSyntaxTree = rewriteResult.SyntaxTree.WithRootAndOptions(rewriteResult.SyntaxTree.GetRoot(), tree.Options);
                        modifiedCompilation = modifiedCompilation.ReplaceSyntaxTree(tree, fixedRewriteResultSyntaxTree);

                        typeCaster.SemanticModel = modifiedCompilation.GetSemanticModel(fixedRewriteResultSyntaxTree);
                        var syntaxTreeTypeRecast = typeCaster.Visit(fixedRewriteResultSyntaxTree.GetRoot());
                        //add inclusion of the BespokeLAttice namespace at the root
                        var compilationUnit = syntaxTreeTypeRecast.SyntaxTree.GetRoot() as CompilationUnitSyntax;

                        UsingDirectiveSyntax newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("BespokeLattice"));
                        SyntaxTree newTree;
                        if (compilationUnit.Usings.Count() > 0)
                        {
                            newTree = CSharpSyntaxTree.Create(compilationUnit.InsertNodesAfter(compilationUnit.Usings[0], new[] { newUsing }).NormalizeWhitespace());
                        }
                        else
                        {
                            newTree = CSharpSyntaxTree.Create(
                            compilationUnit.AddUsings(
                                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("BespokeLattice"))
                            ).NormalizeWhitespace()
                            );
                        }

                        //modifiedCompilation = modifiedCompilation.ReplaceSyntaxTree(rewriteResult.SyntaxTree, newTree);

                        File.WriteAllText(newSourceFile, newTree.GetRoot().ToFullString());
                        finalTree = CSharpSyntaxTree.ParseText(File.ReadAllText(newSourceFile), path: newSourceFile);
                        fixedFinalTree = finalTree.WithRootAndOptions(finalTree.GetRoot(), tree.Options);

                        modifiedCompilation = modifiedCompilation.ReplaceSyntaxTree(fixedRewriteResultSyntaxTree, fixedFinalTree);
                    }

                    var newSemanticModel = modifiedCompilation.GetSemanticModel(fixedFinalTree);
                    foreach (var d in newSemanticModel.GetDiagnostics())
                    {
                        if (d.WarningLevel == 0)
                        {
                            Console.WriteLine(d);
                            if (ErrorHistogram.ContainsKey(d.Id))
                            {
                                ErrorHistogram[d.Id].Add(new Tuple<Diagnostic, string>(d, d.GetMessage()));
                            }
                            else
                            {
                                ErrorHistogram.Add(d.Id, new List<Tuple<Diagnostic, string>> { new Tuple<Diagnostic, string>(d, d.GetMessage()) });
                            }
                        }
                    }
                }
            }
        }
    }

    class RewriteWalker : CSharpSyntaxRewriter
    {
        private Dictionary<SyntaxNode, int> _methodRewrites = new Dictionary<SyntaxNode, int>();
        private Dictionary<SyntaxNode, int> _variableRewrites = new Dictionary<SyntaxNode, int>();
        private readonly string _typename;
        public SemanticModel SemanticModel { get; set; }
        public Dictionary<string, int> VariableSymbols = new Dictionary<string, int>();

        public bool Rewrote { set; get; }

        public RewriteWalker(string typename)
        {
            _typename = typename;
        }

        public void InitRewrites(List<HashSet<AbstractNode>> clusters)
        {
            int clusterID = 0;
            foreach (var color in clusters)
            {
                foreach (var node in color)
                {
                    if (node is MethodReturnSymbol returnSymbol)
                    {
                        _methodRewrites[returnSymbol.Symbol.DeclaringSyntaxReferences.First().GetSyntax()] = clusterID;
                    }
                    else if (node is VariableSymbol s)
                    {
                        SyntaxNode n = s.Symbol.DeclaringSyntaxReferences.First().GetSyntax();
                        if (n is VariableDeclaratorSyntax)
                        {
                            _variableRewrites[n.Parent] = clusterID;
                        }
                        else
                        {
                            _variableRewrites[n] = clusterID;
                        }

                        try
                        {
                            VariableSymbols[n.Kind().ToString()]++;
                        }
                        catch (KeyNotFoundException)
                        {
                            VariableSymbols.Add(n.Kind().ToString(), 1);
                        }
                    }
                }
                clusterID++;
            }
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var method = SemanticModel.GetDeclaredSymbol(node);
            if (method.ExplicitInterfaceImplementations != null) return base.VisitMethodDeclaration(node);

            if (_methodRewrites.TryGetValue(node, out int clusterID))
            {
                SyntaxNode n = node.WithReturnType(SyntaxFactory.ParseTypeName("Cluster" + clusterID.ToString()));
                Rewrote = true;
                return n;
            }

            return base.VisitMethodDeclaration(node);
        }

        public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (node.Parent is LocalDeclarationStatementSyntax localDeclarationStatement)
            {
                if (localDeclarationStatement.Modifiers.Any(SyntaxKind.ConstKeyword)) return base.VisitVariableDeclaration(node);
            }
            else if (node.Parent is FieldDeclarationSyntax fieldDeclaration)
            {
                if (fieldDeclaration.Modifiers.Any(SyntaxKind.ConstKeyword)) return base.VisitVariableDeclaration(node);
            }
            /*else if (node.Parent is PropertyDeclarationSyntax)
            {
                var n = node.Parent as PropertyDeclarationSyntax;
                if (n.Modifiers.Any(SyntaxKind.ConstKeyword)) return base.VisitVariableDeclaration(node);
            }*/
            if (node.Ancestors().Any(x => x is LambdaExpressionSyntax || x is ParenthesizedLambdaExpressionSyntax))
            {
                return base.VisitVariableDeclaration(node);
            }
            if (_variableRewrites.TryGetValue(node, out int clusterID))
            {
                SyntaxNode n = node.WithType(SyntaxFactory.ParseTypeName("Cluster" + clusterID.ToString()));
                Rewrote = true;
                return n;
            }

            return base.VisitVariableDeclaration(node);
        }

        public override SyntaxNode VisitParameter(ParameterSyntax node)
        {
            if (node.Parent.Parent is MethodDeclarationSyntax methodDeclaration)
            {
                var method = SemanticModel.GetDeclaredSymbol(methodDeclaration);
                if (method.ExplicitInterfaceImplementations != null) return base.VisitParameter(node);
            }

            var kind = SemanticModel.GetDeclaredSymbol(node).RefKind;

            if (kind != RefKind.Ref && kind != RefKind.Out)
            {
                if (_variableRewrites.TryGetValue(node, out int clusterID) && !node.Modifiers.Contains(SyntaxFactory.Token(SyntaxKind.RefKeyword)))
                {
                    TypeSyntax type = SyntaxFactory.ParseTypeName("Cluster" + clusterID.ToString());
                    ParameterSyntax n = node.Update(node.AttributeLists, node.Modifiers, type, node.Identifier, node.Default);
                    return n;
                }
            }

            return base.VisitParameter(node);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var prop = SemanticModel.GetDeclaredSymbol(node);
            if (prop.ExplicitInterfaceImplementations != null) return base.VisitPropertyDeclaration(node);

            if (_variableRewrites.TryGetValue(node, out int clusterID))
            {
                SyntaxNode n = node.WithType(SyntaxFactory.ParseTypeName("Cluster" + clusterID.ToString()));
                Rewrote = true;
                return n;
            }
            return base.VisitPropertyDeclaration(node);
        }
    }

    public class TypeCaster : CSharpSyntaxRewriter
    {
        public SemanticModel SemanticModel { get; set; }
        private readonly string _typename;

        public TypeCaster(string typename)
        {
            _typename = typename;
        }

        public override SyntaxNode VisitArgument(ArgumentSyntax node)
        {
            var type = SemanticModel.GetTypeInfo(node.Expression).Type as ITypeSymbol;
            if (type != null && type.IsReferenceType && type.Name.Contains("Cluster"))
            {
                return node.WithExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        node.Expression,
                        SyntaxFactory.IdentifierName(
                            @"val")
                    ).WithOperatorToken(
                        SyntaxFactory.Token(SyntaxKind.DotToken))
                    );
            }
            return base.VisitArgument(node);
        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (SemanticModel.GetTypeInfo(node.Expression).Type is ITypeSymbol type && type.IsReferenceType && type.Name.Contains("Cluster"))
            {
                return node.WithExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        node.Expression,
                        SyntaxFactory.IdentifierName(
                            @"val")
                    ).WithOperatorToken(
                        SyntaxFactory.Token(SyntaxKind.DotToken))
                    );
            }
            return base.VisitElementAccessExpression(node);
        }
        /*
        public override SyntaxNode VisitRefExpression(RefExpressionSyntax node)
        {
            var t = SemanticModel.GetTypeInfo(node).Type.Name;
            if (t.Contains("Cluster"))
            {
                return node.WithExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(_typename), node.Expression));
            }
            return base.VisitRefExpression(node);
        }
        */
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            try
            {
                var typename = SemanticModel.GetTypeInfo(node.Expression).Type.Name;
                if (typename.Contains("Cluster"))
                {
                    return node.WithExpression(
                        SyntaxFactory.ParenthesizedExpression(
                            SyntaxFactory.CastExpression(
                                SyntaxFactory.ParseTypeName(_typename), node.Expression)));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.Message);
            }
            return base.VisitMemberAccessExpression(node);
        }
    }

}
