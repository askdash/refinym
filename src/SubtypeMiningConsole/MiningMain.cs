using System;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using System.Collections.Generic;

namespace SubtypeInduction.TypeSystemRels
{
    public class MiningMain
    {

        public static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage cluster <PathToSln> <repositoryPath> <gitHubLink> <OutputFileName> <typeToCluster>");
                Console.WriteLine("Usage rewrite <PathToSln> <repositoryPath> <gitHubLink> <OutputFileName>");
                return;
            }
            
            MSBuildLocator.RegisterDefaults();

            var workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { { "DebugSymbols", "False" } });
            workspace.SkipUnrecognizedProjects = true;

            workspace.WorkspaceFailed += (e, o) =>
                Console.WriteLine(o.Diagnostic.ToString());
            var solution = workspace.OpenSolutionAsync(args[1]).Result;

            switch(args[0])
            {
                case "cluster":
                    ClusteringExtractor.ExtractFromSolution(args[2], args[3], solution, args[4], args[5]);
                    break;
                case "rewrite":
                    // TODO(Santanu): Fix operations here
                    SubtypeMiner.ExtractFromSolution(args[1], args[2], solution);
                    break;
                default:
                    throw new Exception($"Unrecognized option {args[0]}");
            }            
        }        
    }
}
