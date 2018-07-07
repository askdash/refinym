using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Newtonsoft.Json;

namespace SubtypeInduction.Utils
{
    public class ScopeExtractor
    {
        private readonly Dictionary<string, ScopeData> _inScopeSymbols = new Dictionary<string, ScopeData>();
        private readonly string _repositoryPath;
        private readonly string _targetType;

        public void WriteJson(string filename)
        {
            File.WriteAllText(filename, JsonConvert.SerializeObject(_inScopeSymbols));
        }

        public ScopeExtractor(string repositoryPath, string targetType)
        {
            _repositoryPath = repositoryPath;
            _targetType = targetType;
        }
        
        public static void ExtractFromSolution(string[] args, Solution solution)
        {
            var projectGraph = solution.GetProjectDependencyGraph();
            var scopeExtractor = new ScopeExtractor(args[1], args[2]);
            var visitedFiles = new HashSet<string>();

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
                var cSharpCompilation = compilation as CSharpCompilation;
                foreach (var error in compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                {
                    Console.WriteLine(error.GetMessage());
                }
                if (cSharpCompilation != null)
                {
                    scopeExtractor.ExtractFrom(cSharpCompilation, ref visitedFiles);
                }
            }
            Console.WriteLine("Writing to output...");
            scopeExtractor.WriteJson(args[3]);
        }

        public void ExtractFrom(CSharpCompilation cSharpCompilation, ref HashSet<string> visitedFiles)
        {
            foreach (var tree in cSharpCompilation.SyntaxTrees)
            {
                if (!visitedFiles.Add(tree.FilePath)) continue;
                var semanticModel = cSharpCompilation.GetSemanticModel(tree);
                AddScopeDataFrom(tree, semanticModel);
            }
        }

        private void AddScopeDataFrom(SyntaxTree tree, SemanticModel semanticModel)
        {
            var extractor = new VariableScopeExtractor(semanticModel, _inScopeSymbols, GetLocation, _targetType);
            extractor.Visit(tree.GetRoot());
        }

        public string GetLocation(Location location, string name)
        {
            var lineSpan = location.GetMappedLineSpan();
            return $"{location.SourceTree.FilePath.Substring(_repositoryPath.Length)}:{lineSpan.StartLinePosition}->{lineSpan.EndLinePosition}:{name}";
        }
    }

    public struct ScopeData
    {
        public ScopeData(string currentSymbol, HashSet<string> symbolsInScope)
        {
            CurrentSymbol = currentSymbol;
            SymbolsInScope = symbolsInScope;
        }

        public readonly string CurrentSymbol;
        public readonly HashSet<string> SymbolsInScope;
    }

    class VariableScopeExtractor : CSharpSyntaxWalker
    {
        private readonly SemanticModel _semanticModel;
        private readonly Dictionary<string, ScopeData> _inScopeSymbols;
        private readonly Func<Location, string, string> _tokenToLocation;
        private readonly string _targetType;

        public VariableScopeExtractor(SemanticModel semanticModel, Dictionary<string, ScopeData> inScopeSymbols,
            Func<Location, string, string> tokenToLocation, string targetType)
        {
            _semanticModel = semanticModel;
            _inScopeSymbols = inScopeSymbols;
            _targetType = targetType;
            _tokenToLocation = tokenToLocation;
        }

        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            var refSymbol = RoslynUtils.GetReferenceSymbol(node, _semanticModel);
            if (refSymbol != null && RoslynUtils.IsVariableSymbol(refSymbol) && refSymbol.Locations[0].IsInSource)
            {
                var typeSymbol = RoslynUtils.GetVariableTypeSymbol(refSymbol);
                if (typeSymbol.ToString() == _targetType)
                {
                    _inScopeSymbols.Add(_tokenToLocation(node.GetLocation(), node.ToString()),
                        new ScopeData(SymbolToString(refSymbol),
                        new HashSet<String>(_semanticModel.LookupSymbols(node.GetLocation().SourceSpan.End).Where(s => RoslynUtils.IsVariableSymbol(s)).
                        Where(s => s.Locations[0].IsInSource).
                        Where(s => RoslynUtils.GetVariableTypeSymbol(s).ToString() == _targetType).Select(s => SymbolToString(s)))));
                }
            }
            base.VisitIdentifierName(node);
        }

        private string SymbolToString(ISymbol symbol)
        {
            return _tokenToLocation(symbol.Locations[0], symbol.Name);
        }
    }
}
