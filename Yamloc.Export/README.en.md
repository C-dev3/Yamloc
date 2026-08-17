# Yamloc

A lightweight, YAML-based .NET localization library inspired by [CheapLoc](https://github.com/goatcorp/Dalamud.CheapLoc). It uses human-editable YAML instead of JSON, so translators can leave comments and context directly alongside each string.

[日本語版 README はこちら](./README.md)

## Packages

This repository is split into two NuGet packages:

| Package | Role | Dependencies |
|---|---|---|
| [`Yamloc`](#yamloc-1) | Run-time localization API (`Setup` / `Localize` / `T`) | YamlDotNet only |
| [`Yamloc.Export`](#yamlocexport) | Build-time tooling that scans a compiled assembly's IL and generates a starter YAML file | YamlDotNet, Mono.Cecil, `Yamloc` |

If all you need is to plug localized strings into your UI at run time, `Yamloc` alone is enough. `Yamloc.Export` is only needed if you want to automatically collect `Loc.T(...)` calls from your source code into a translation-ready YAML template. It depends on Mono.Cecil, so it is intentionally kept out of the run-time package (Mono.Cecil can be awkward under trimming/AOT).

## Yamloc

### Installation

```
dotnet add package Yamloc
```

### Basic usage

**1. Load localization data at startup**

```csharp
using Yamloc;

// Load from a file
Loc.SetupFromFile("path/to/ja.yml");

// Or pass a YAML string directly (e.g. from an embedded resource)
Loc.Setup(yamlString);
```

`Setup` / `SetupFromFile` use `Assembly.GetCallingAssembly()` to determine the calling assembly automatically, and keep each assembly's localization data in its own isolated namespace. Even if multiple DLLs (e.g. plugins) are loaded into the same process, their keys won't collide.

**2. Localize a string**

```csharp
using Yamloc;

// If the key isn't found, the fallback is shown as-is
string label = Loc.T("menu.save", "Save");

// string.Format-compatible argument overload is also available
string greeting = Loc.T("greeting", "Hello, {0}!", playerName);
```

`fallback` should be the string in your source language (the language you develop against). It's shown as-is whenever translation data hasn't been provided yet, so you can keep building your UI without waiting for translations to be complete.

If `Setup` has never been called for the calling assembly, `Loc.T` returns `#key`, making it obvious on screen that localization hasn't been set up yet.

### YAML file format

```yaml
menu.save:
  message: "Save"
  description: "Save button in the main menu"

greeting:
  message: "Hello, {0}!"
  description: "{0}=player name"
```

Each key has a `message` (the string actually shown) and a `description` (context for translators).

If part of a translation file (a single key) is malformed, the rest of the keys that parsed successfully are still loaded correctly. Only the broken key falls back, so a single mistake in the file doesn't disable localization for the whole application.

### API reference

| Method | Description |
|---|---|
| `Loc.Setup(string locData)` | Set up localization data for the calling assembly from a YAML string |
| `Loc.Setup(string locData, Assembly assembly)` | Set up localization data for a specific assembly from a YAML string |
| `Loc.SetupFromFile(string path)` | Set up localization data for the calling assembly by loading a YAML file |
| `Loc.SetupFromFile(string path, Assembly assembly)` | Set up localization data for a specific assembly by loading a YAML file |
| `Loc.SetupWithFallbacks()` / `Loc.SetupWithFallbacks(Assembly)` | Set up empty localization data, forcing all fallback strings to display |
| `Loc.Localize(string key, string fallBack)` / `Loc.T(string key, string fallBack)` | Resolve a localized string (`T` is a shorthand alias for `Localize`) |
| `Loc.Localize(string key, string fallBack, params object[] args)` / `Loc.T(string key, string fallBack, params object[] args)` | Resolve a localized string with `string.Format`-style arguments |

## Yamloc.Export

A build-time tool that statically scans a compiled assembly's IL for calls to `Loc.Localize(...)` / `Loc.T(...)` and generates a starter translation YAML file. It uses Mono.Cecil to analyze IL, so it is not needed at run time.

### Installation

```
dotnet add package Yamloc.Export
```

### Usage

Run it against a built assembly (e.g. from a build script or a separate CLI project):

```csharp
using Yamloc.Export;
using System.Reflection;

var assembly = Assembly.LoadFrom("path/to/YourPlugin.dll");
LocExporter.ExportLocalizableForAssembly(assembly);
```

A shorthand that infers the calling assembly automatically is also available:

```csharp
LocExporter.ExportLocalizable();
```

This collects every `Loc.T(key, fallback, ...)` / `Loc.Localize(key, fallback, ...)` call in the code and writes them out to `{AssemblyName}_Localizable.yaml`. A `loc.log` file is also written, showing which method each key was found in for debugging.

### Re-exporting while keeping existing translations

Pass an already-translated YAML file via `existingYamlPath` to merge against it instead of overwriting it:

```csharp
LocExporter.ExportLocalizableForAssembly(
    assembly,
    existingYamlPath: "ja.yml");
```

In this case:

- For keys that still exist in the code and already have a translated `message` in the existing file, the translation is preserved and only `description` is refreshed to point at the current code location
- Keys that no longer appear in the code are dropped from the output
- Newly added keys are exported with the source-language `fallback` string as their `message`, marking them as untranslated

This means the YAML file can be regenerated as the code evolves — call sites move, keys get added or removed — without losing translators' completed work.

### Constraints & caveats

- If the same key is used with a different `fallback` string at different call sites, an exception is thrown during export (there's no way to know which one is correct). Always use the same fallback for the same key.
- Calls where the key resolves to `null` or an empty string throw an exception by default. Pass `ignoreInvalidFunctions: true` to skip such call sites and continue the export instead.
- Calls with format arguments (`Loc.T(key, fallback, args)`) are supported, but detection relies on recognizing a specific IL pattern (array-construction instructions). Both `fallback` and `key` must be compile-time string literals — dynamically built strings cannot be detected.

## License

MIT License
