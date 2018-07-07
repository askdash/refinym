using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SubtypeInduction
{
    class RoslynUtils
    {
        /// <summary>
        /// Get the symbol of a given node.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="semanticModel"></param>
        /// <returns></returns>
        public static ISymbol GetReferenceSymbol(SyntaxNode node, SemanticModel semanticModel)
        {
            ISymbol identifierSymbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (identifierSymbol == null && node.Kind() == SyntaxKind.VariableDeclarator)
            {
                identifierSymbol = semanticModel.GetDeclaredSymbol(((VariableDeclaratorSyntax)node));
            }
            else if (identifierSymbol == null && node.Kind() == SyntaxKind.ForEachStatement)
            {
                identifierSymbol = semanticModel.GetDeclaredSymbol(((ForEachStatementSyntax)node));
            }
            else if (identifierSymbol != null)
            {
                identifierSymbol = identifierSymbol.OriginalDefinition;
            }
            return identifierSymbol;
        }

        /// <summary>
        /// Get the type symbol for a given variable.
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static ITypeSymbol GetVariableTypeSymbol(ISymbol symbol)
        {
            ITypeSymbol type;
            if (symbol is ILocalSymbol)
            {
                type = ((ILocalSymbol)symbol).Type;
            }
            else if (symbol is IFieldSymbol)
            {
                type = ((IFieldSymbol)symbol).Type;
            }
            else if (symbol is IParameterSymbol)
            {
                type = ((IParameterSymbol)symbol).Type;
            }
            else if (symbol is IRangeVariableSymbol)
            {
                type = null;
            }
            else if (symbol is IPropertySymbol)
            {
                type = ((IPropertySymbol)symbol).Type;
            }
            else
            {
                return null;
            }
            return type;
        }

        public static bool IsVariableSymbol(ISymbol symbol)
        {
            var symbolKind = symbol.Kind;
            return (symbolKind == SymbolKind.Local || symbolKind == SymbolKind.Parameter ||
                    symbolKind == SymbolKind.Field || symbolKind == SymbolKind.Property);
        }
    }
}

