using System;
using System.IO;

namespace Yamloc.Export.Tool
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string assemblyPath = null;
            string existingYamlPath = null;
            bool ignoreInvalidFunctions = false;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "-h":
                    case "--help":
                        PrintUsage();
                        return 0;

                    case "-y":
                    case "--yaml":
                    case "--existing-yaml":
                        if (i + 1 >= args.Length)
                        {
                            Console.Error.WriteLine($"Error: option '{arg}' requires a value.");
                            return 1;
                        }
                        existingYamlPath = args[++i];
                        break;

                    case "-i":
                    case "--ignore-invalid":
                    case "--ignore-invalid-functions":
                        ignoreInvalidFunctions = true;
                        break;

                    default:
                        if (arg.StartsWith('-'))
                        {
                            Console.Error.WriteLine($"Error: unknown option '{arg}'.");
                            PrintUsage();
                            return 1;
                        }

                        if (assemblyPath != null)
                        {
                            Console.Error.WriteLine("Error: multiple assembly paths were specified; only one is supported.");
                            PrintUsage();
                            return 1;
                        }

                        assemblyPath = arg;
                        break;
                }
            }

            if (string.IsNullOrEmpty(assemblyPath))
            {
                Console.Error.WriteLine("Error: an assembly path is required.");
                PrintUsage();
                return 1;
            }

            if (!File.Exists(assemblyPath))
            {
                Console.Error.WriteLine($"Error: assembly not found at '{assemblyPath}'.");
                return 1;
            }

            if (!string.IsNullOrEmpty(existingYamlPath) && !File.Exists(existingYamlPath))
            {
                Console.WriteLine($"Note: '{existingYamlPath}' does not exist yet; a new YAML file will be created there.");
            }

            try
            {
                LocExporter.ExportLocalizableForAssembly(assemblyPath, existingYamlPath, ignoreInvalidFunctions);

                var outputPath = existingYamlPath ?? $"{Path.GetFileNameWithoutExtension(assemblyPath)}_Localizable.yaml";
                Console.WriteLine($"Localization data exported to '{Path.GetFullPath(outputPath)}'.");
                Console.WriteLine($"Debug log written to '{Path.GetFullPath("loc.log")}'.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Export failed: {ex.Message}");
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
            @"Yamloc.Export.Tool - extracts localizable strings from a compiled assembly into a YAML file.

            Usage:
              yamloc-export <assemblyPath> [options]

            Arguments:
              <assemblyPath>                 Path to the compiled assembly (.dll/.exe) to scan.

            Options:
              -y, --yaml <path>              Path to an existing/translated YAML file to merge against.
                                              Keys still found in the code keep their existing translated
                                              message instead of being overwritten. If omitted, a new
                                              '<AssemblyName>_Localizable.yaml' is written to the current
                                              directory.
              -i, --ignore-invalid           Ignore malformed Localize/T calls instead of failing.
              -h, --help                     Show this help message.

            Examples:
              yamloc-export ./bin/MyGame.dll
              yamloc-export ./bin/MyGame.dll --yaml ./Localization/MyGame_ja.yaml --ignore-invalid");
        }
    }
}