using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using OpCodes = Mono.Cecil.Cil.OpCodes;

namespace Yamloc.Export
{
    /// <summary>
    /// Build-time tooling for generating localizable YAML files from a compiled assembly.
    /// Scans the assembly's IL (via Mono.Cecil) for calls to <see cref="Loc.Localize(string, string)"/>
    /// / <see cref="Loc.T(string, string)"/> and writes out a key/LocEntry YAML mapping.
    ///
    /// Kept in a separate package from <c>Yamloc</c> because it depends on Mono.Cecil, which is only
    /// needed at development/build time - not something run-time consumers of <c>Loc.Localize()</c>
    /// should have to carry as a dependency (and Mono.Cecil can be awkward under trimming/AOT).
    /// </summary>
    public static class LocExporter
    {
        private static readonly ISerializer YamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// Names of the methods on <see cref="Loc"/> that resolve a localized string and should be
        /// picked up by <see cref="ExportLocalizableForAssembly(string, string, bool)"/>. <c>T</c> is included alongside
        /// <c>Localize</c> since it is only a shorthand alias for it at the IL level.
        /// </summary>
        private static readonly HashSet<string> LocalizeMethodNames = new HashSet<string> { "Localize", "T" };

        /// <summary>
        /// Saves localizable YAML data in the current working directory for the provided assembly.
        /// If <paramref name="existingYamlPath"/> points to an already-translated file, any keys that
        /// still exist after re-scanning the code will keep their existing translated <see cref="LocEntry.Message"/>
        /// instead of being overwritten by the source-language fallback. Keys that no longer appear in the
        /// code are dropped, and the <see cref="LocEntry.Description"/> is always refreshed to the latest location.
        /// </summary>
        /// <param name="assemblyPath">Assembly path to save localization data from.</param>
        /// <param name="existingYamlPath">
        /// Optional path to a previously exported/translated YAML file to merge against.
        /// If null or the file does not exist, this behaves like a fresh export.
        /// </param>
        /// <param name="ignoreInvalidFunctions">If set to true, this ignores malformed Localize functions instead of failing.</param>
        public static void ExportLocalizableForAssembly(
            string assemblyPath, string existingYamlPath = null, bool ignoreInvalidFunctions = false)
        {
            var existing = LoadExistingLocEntries(existingYamlPath);

            // StringBuilder instead of repeated string concatenation, which would otherwise
            // allocate a new string on every += inside the instruction loop below.
            var debugOutput = new StringBuilder();
            var outList = new Dictionary<string, LocEntry>();

            var assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath);
            var assemblyName = assemblyDef.Name.Name;

            var toInspect = assemblyDef.MainModule.GetTypes()
                .SelectMany(t => t.Methods
                    .Where(m => m.HasBody)
                    .Select(m => new { t, m }));

            foreach (var tm in toInspect)
            {
                var instructions = tm.m.Body.Instructions;

                foreach (var instruction in instructions)
                {
                    if (instruction.OpCode == OpCodes.Call)
                    {
                        if (instruction.Operand is MethodReference methodInfo)
                        {
                            var methodType = methodInfo.DeclaringType;
                            var parameters = methodInfo.Parameters;

                            // Only consider calls to Loc.Localize(...) / Loc.T(...) themselves.
                            // Matching on "Name.Contains("Localize")" alone would silently miss
                            // Loc.T(...) calls (T does not contain "Localize"), and matching on
                            // name only (without DeclaringType) could false-positive on an
                            // unrelated type's own "T" or "Localize" method.
                            if (methodType.FullName != typeof(Loc).FullName
                                || !LocalizeMethodNames.Contains(methodInfo.Name))
                                continue;

                            debugOutput.AppendFormat("->{0}.{1}.{2}({3});\n",
                                    tm.t.FullName,
                                    methodType.Name,
                                    methodInfo.Name,
                                    string.Join(", ",
                                        parameters.Select(p =>
                                            p.ParameterType.FullName + " " + p.Name).ToArray())
                                );

                            // For Loc.Localize(key, message) / Loc.T(key, message) the IL is simply
                            // "ldstr key; ldstr message; call", so the two instructions immediately
                            // preceding the call are the message and key.
                            //
                            // For the params object[] overloads called WITH actual format arguments
                            // (e.g. Loc.T("Key", "Hi {0}", name)), the compiler builds the array
                            // between the two ldstr pushes and the call:
                            //   ldstr key; ldstr message; ldc.i4 <n>; newarr; [dup; ldc.i4 idx; value; stelem]*; call
                            // so we first have to walk back past that array-construction sequence to
                            // reach the message/key pushes. We detect this case via the declared
                            // parameter list rather than by opcode-sniffing forward, since an argument
                            // value could itself legitimately be a string literal.
                            var isParamsArgsCall = parameters.Count > 0 && parameters[parameters.Count - 1].ParameterType.IsArray;

                            Instruction messageInstruction;
                            Instruction keyInstruction;

                            if (isParamsArgsCall)
                            {
                                var cursor = instruction.Previous;
                                while (cursor != null && cursor.OpCode != OpCodes.Newarr)
                                    cursor = cursor.Previous;

                                // Step past "newarr" and the array-length "ldc.i4" push before it
                                // to land on the "ldstr message" push.
                                messageInstruction = cursor?.Previous?.Previous;
                                keyInstruction = messageInstruction?.Previous;
                            }
                            else
                            {
                                messageInstruction = instruction.Previous;
                                keyInstruction = instruction.Previous.Previous;
                            }

                            var entry = new LocEntry
                            {
                                Message = messageInstruction?.Operand as string,
                                Description = $"{tm.t.Name}.{tm.m.Name}",
                            };

                            var key = keyInstruction?.Operand as string;

                            if (string.IsNullOrEmpty(key))
                            {
                                var errMsg = $"Key was empty for message: {entry.Message} (from {entry.Description}) in {tm.t.FullName}::{tm.m.FullName}";
                                if (ignoreInvalidFunctions)
                                {
                                    debugOutput.Append(errMsg).Append('\n');
                                    continue;
                                }
                                else
                                    throw new Exception(errMsg);
                            }

                            // Single dictionary lookup (O(1)) instead of two LINQ Any() scans (O(n) each)
                            // over outList for every instruction that references the same key.
                            if (outList.TryGetValue(key, out var existingOutEntry))
                            {
                                if (existingOutEntry.Message != entry.Message)
                                {
                                    throw new Exception(
                                        $"Message with key {key} has previous appearance but other fallback text in {entry.Description} in {tm.t.FullName}::{tm.m.FullName}");
                                }
                            }
                            else
                            {
                                if (existing.TryGetValue(key, out var existingEntry)
                                    && !string.IsNullOrEmpty(existingEntry.Message))
                                {
                                    // Keep the already-translated message, but refresh the
                                    // description so it still points at the current code location.
                                    entry.Message = existingEntry.Message;
                                    debugOutput.Append($"    ->{key} - kept existing translation (from {entry.Description})\n");
                                }
                                else
                                {
                                    debugOutput.Append($"    ->{key} - {entry.Message} (from {entry.Description})\n");
                                }

                                outList.Add(key, entry);
                            }
                        }
                    }
                }
            }

            var outputPath = existingYamlPath ?? $"{assemblyName}_Localizable.yaml";

            File.WriteAllText("loc.log", debugOutput.ToString());
            File.WriteAllText(outputPath, YamlSerializer.Serialize(outList));
        }

        /// <summary>
        /// Saves localizable YAML data in the current working directory for the provided assembly.
        /// This is a convenience overload that resolves the assembly's file path and forwards to
        /// <see cref="ExportLocalizableForAssembly(string, string, bool)"/>.
        /// </summary>
        /// <param name="assembly">Assembly to save localization data from.</param>
        /// <param name="existingYamlPath">
        /// Optional path to a previously exported/translated YAML file to merge against.
        /// If null or the file does not exist, this behaves like a fresh export.
        /// </param>
        /// <param name="ignoreInvalidFunctions">If set to true, this ignores malformed Localize functions instead of failing.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExportLocalizableForAssembly(Assembly assembly, string existingYamlPath = null, bool ignoreInvalidFunctions = false) => ExportLocalizableForAssembly(assembly.Location, existingYamlPath, ignoreInvalidFunctions);

        /// <summary>
        /// Saves localizable YAML data in the current working directory for the calling assembly.
        /// See <see cref="ExportLocalizableForAssembly(string, string, bool)"/> for details on the merge behavior.
        /// </summary>
        /// <param name="existingYamlPath">
        /// Optional path to a previously exported/translated YAML file to merge against.
        /// If null or the file does not exist, this behaves like a fresh export.
        /// </param>
        /// <param name="ignoreInvalidFunctions">If set to true, this ignores malformed Localize functions instead of failing.</param>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ExportLocalizable(string existingYamlPath = null, bool ignoreInvalidFunctions = false) => ExportLocalizableForAssembly(Assembly.GetCallingAssembly(), existingYamlPath, ignoreInvalidFunctions);

        /// <summary>
        /// Loads a previously exported YAML file into a key/LocEntry map, for merging during export.
        /// Returns an empty map if the path is null, empty, or the file does not exist.
        /// </summary>
        private static Dictionary<string, LocEntry> LoadExistingLocEntries(string existingYamlPath)
        {
            if (string.IsNullOrEmpty(existingYamlPath) || !File.Exists(existingYamlPath))
                return new Dictionary<string, LocEntry>();

            var yaml = File.ReadAllText(existingYamlPath);

            return string.IsNullOrWhiteSpace(yaml)
                ? new Dictionary<string, LocEntry>()
                : YamlDeserializer.Deserialize<Dictionary<string, LocEntry>>(yaml)
                  ?? new Dictionary<string, LocEntry>();
        }
    }
}