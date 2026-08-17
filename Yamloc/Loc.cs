using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Yamloc
{
    /// <summary>
    /// Static class providing run-time localization services, backed by YAML data.
    /// This is a YAML-based counterpart to CheapLoc, intended as a drop-in alternative
    /// for projects that prefer human-editable YAML over JSON for translation files.
    ///
    /// This package only contains the run-time lookup APIs and depends solely on YamlDotNet.
    /// For the build-time export tooling (<c>ExportLocalizable</c>/<c>ExportLocalizableForAssembly</c>,
    /// which additionally depends on Mono.Cecil), see the separate <c>Yamloc.Export</c> package.
    /// </summary>
    public static class Loc
    {
        // ConcurrentDictionary so that Setup()/SetupFromFile() on one thread can safely swap in
        // a new per-assembly dictionary while another thread concurrently calls Localize()/T().
        // Each inner Dictionary<string, LocEntry> is only ever replaced wholesale (never mutated
        // in place after being published here), so no additional locking is required for reads.
        internal static readonly ConcurrentDictionary<string, Dictionary<string, LocEntry>> LocData = new ConcurrentDictionary<string, Dictionary<string, LocEntry>>();

        internal static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        /// <summary>
        /// Set-up localization data for the calling assembly by loading a YAML file from disk.
        /// </summary>
        /// <param name="path">Path to a YAML file containing a key/LocEntry mapping.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when no file exists at <paramref name="path"/>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file's content is not valid YAML for this schema.</exception>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetupFromFile(string path) => SetupFromFile(path, Assembly.GetCallingAssembly());

        /// <summary>
        /// Set-up localization data for the provided assembly by loading a YAML file from disk.
        /// </summary>
        /// <param name="path">Path to a YAML file containing a key/LocEntry mapping.</param>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when no file exists at <paramref name="path"/>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file's content is not valid YAML for this schema.</exception>
        // NOTE: does not call GetCallingAssembly(), so it is safe to let the JIT inline this.
        public static void SetupFromFile(string path, Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path must not be null or empty.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Localization file not found: {path}", path);

            string locData;
            try
            {
                locData = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to read localization file: {path}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException($"Failed to read localization file: {path}", ex);
            }

            try
            {
                Setup(locData, assembly);
            }
            catch (YamlException ex)
            {
                throw new InvalidDataException($"Failed to parse localization YAML at {path}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Set-up localization data for the calling assembly with the provided YAML structure.
        /// </summary>
        /// <param name="locData">YAML structure containing a key/LocEntry mapping.</param>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Setup(string locData) => Setup(locData, Assembly.GetCallingAssembly());

        /// <summary>
        /// Set-up localization data for the provided assembly with the provided YAML structure.
        /// </summary>
        /// <param name="locData">YAML structure containing a key/LocEntry mapping.</param>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        // NOTE: does not call GetCallingAssembly(), so it is safe to let the JIT inline this.
        public static void Setup(string locData, Assembly assembly)
        {
            var assemblyName = GetAssemblyName(assembly);

            var deserialized = string.IsNullOrWhiteSpace(locData)
                ? new Dictionary<string, LocEntry>()
                : YamlDeserializer.Deserialize<Dictionary<string, LocEntry>>(locData)
                  ?? new Dictionary<string, LocEntry>();

            // Indexer assignment replaces any existing entry (or adds a new one) in a single
            // lookup, instead of a separate ContainsKey + Remove + Add.
            LocData[assemblyName] = deserialized;
        }

        /// <summary>
        /// Set-up empty localization data to force all fallbacks to show for the calling assembly.
        /// </summary>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetupWithFallbacks() => Setup(string.Empty, Assembly.GetCallingAssembly());

        /// <summary>
        /// Set-up empty localization data to force all fallbacks to show for the provided assembly.
        /// </summary>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        // NOTE: does not call GetCallingAssembly(), so it is safe to let the JIT inline this.
        public static void SetupWithFallbacks(Assembly assembly) => Setup(string.Empty, assembly);

        /// <summary>
        /// Shorthand alias for <see cref="Localize(string, string)"/>.
        /// </summary>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string T(string key, string fallBack) => Localize(key, fallBack, Assembly.GetCallingAssembly());

        /// <summary>
        /// Search the set-up localization data for the provided assembly for the given string key and return it.
        /// If the key is not present, the fallback is shown.
        /// The fallback is also required to create the string files to be localized.
        ///
        /// Calling this method should always be the first step in your localization chain.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>
        /// <returns>The localized string, fallback or string key if not found.</returns>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Localize(string key, string fallBack) => Localize(key, fallBack, Assembly.GetCallingAssembly());

        /// <summary>
        /// Shorthand alias for <see cref="Localize(string, string, object[])"/>.
        /// Search the set-up localization data for the calling assembly for the given string key,
        /// formatting it with the supplied arguments.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>
        /// <param name="args">Optional arguments used to format the resolved string via <see cref="string.Format(string, object[])"/>.</param>
        /// <returns>The localized (or fallback) string, formatted with <paramref name="args"/> if any were provided.</returns>
        // NOTE: this method must call Assembly.GetCallingAssembly() itself (rather than delegating
        // to Localize(string, string, object[]) and letting IT call GetCallingAssembly()). If it
        // delegated, GetCallingAssembly() inside Localize would see T's own assembly (Yamloc itself)
        // as the "caller", not the assembly that actually called T() - silently resolving against
        // the wrong assembly's localization data. Must not be inlined into its caller for the same
        // reason GetCallingAssembly()-calling methods elsewhere in this class are NoInlining.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string T(string key, string fallBack, params object[] args) => Localize(key, fallBack, Assembly.GetCallingAssembly(), args);

        /// <summary>
        /// Search the set-up localization data for the calling assembly for the given string key and return it,
        /// formatting it with the supplied arguments if any are provided.
        /// If the key is not present, the fallback is used as the format string instead.
        /// The fallback is also required to create the string files to be localized.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>
        /// <param name="args">Optional arguments used to format the resolved string via <see cref="string.Format(string, object[])"/>.</param>
        /// <returns>The localized (or fallback) string, formatted with <paramref name="args"/> if any were provided.</returns>
        // NOTE: this method calls Assembly.GetCallingAssembly(), so it must not be inlined into its caller,
        // otherwise GetCallingAssembly() would resolve one frame too high.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string Localize(string key, string fallBack, params object[] args)
            => Localize(key, fallBack, Assembly.GetCallingAssembly(), args);

        /// <summary>
        /// Shared implementation for the <c>params object[]</c> overloads of <see cref="Localize(string, string, object[])"/>
        /// and <see cref="T(string, string, object[])"/>. Both public entry points capture
        /// <see cref="Assembly.GetCallingAssembly"/> themselves and pass it in explicitly, so this
        /// helper itself never needs to call (or care about) GetCallingAssembly().
        /// </summary>
        private static string Localize(string key, string fallBack, Assembly assembly, object[] args)
        {
            var format = Localize(key, fallBack, assembly);
            return args.Length > 0 ? string.Format(format, args) : format;
        }

        /// <summary>
        /// Search the set-up localization data for the calling assembly for the given string key and return it.
        /// If the key is not present, the fallback is shown.
        /// The fallback is also required to create the string files to be localized.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        /// <returns>The localized string, fallback or string key if not found.</returns>
        // NOTE: does not call GetCallingAssembly(), so it is safe to let the JIT inline this.
        public static string Localize(string key, string fallBack, Assembly assembly)
        {
            var assemblyName = GetAssemblyName(assembly);

            // Single lookup instead of ContainsKey followed by an indexer lookup.
            if (!LocData.TryGetValue(assemblyName, out var assemblyLocData))
                return $"#{key}";

            if (!assemblyLocData.TryGetValue(key, out var localizedString))
                return string.IsNullOrEmpty(fallBack) ? $"#{key}" : fallBack;

            return string.IsNullOrEmpty(localizedString.Message) ? $"#{key}" : localizedString.Message;
        }

        internal static string GetAssemblyName(Assembly assembly) => assembly.GetName().Name;
    }
}