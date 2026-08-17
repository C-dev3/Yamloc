# Yamloc

Yamloc is a lightweight, YAML-based localization library for .NET.

This library was built as a reference implementation inspired by [CheapLoc](https://github.com/goaaats/CheapLoc). It carries over CheapLoc's philosophy of being easy to integrate and not requiring a separate localization database, while using human-friendly YAML instead of JSON as the translation file format.
[日本語版 README はこちら](./README.md)

## Features

- **Simple API** — get started with a single call to `Loc.Setup()`
- **YAML-based** — a format that supports comments and is easy to hand-edit
- **Per-assembly data** — automatically resolves the calling assembly and keeps localization data separated per assembly
- **Fallback support** — shows a fallback string when a key is not found
- **Format arguments** — build strings using `string.Format`-style arguments
- **Translator context** — each entry can carry a `description` explaining where the string is used
- **Thread-safe** — data swaps via `Setup()` are safe to run concurrently with `Localize()` calls

Note that this package (`Yamloc`) only contains the run-time localization APIs, and its only dependency is [YamlDotNet](https://github.com/aaubry/YamlDotNet). The build-time tooling for exporting localizable strings (equivalent to `ExportLocalizable` / `ExportLocalizableForAssembly`), which additionally depends on [Mono.Cecil](https://github.com/jbevain/cecil), is planned to be provided as a separate `Yamloc.Export` package.

## Installation

```bash
dotnet add package Yamloc
```

## Usage

### 1. Prepare your translation data (YAML)

`message` is required; `description` is an optional note for translators.

```yaml
Greeting:
  message: "Hello, World!"
  description: "Greeting shown when the app starts"
HelloName:
  message: "Hello, {0}."
  description: "Greeting that includes the user's name (one argument)"
```

### 2. Initialize

Call `Loc.Setup()` / `Loc.SetupFromFile()`, typically when your app starts.

```csharp
using Yamloc;

var allowedLang = new[] { "de", "ja", "fr", "it", "es" };
var currentUiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

if (allowedLang.Contains(currentUiLang))
{
    Loc.SetupFromFile($"loc_{currentUiLang}.yaml");
}
else
{
    // No matching language data available - fall back to the fallback strings only
    Loc.SetupWithFallbacks();
}
```

You can also pass a YAML string directly.

```csharp
Loc.Setup(File.ReadAllText("loc_en.yaml"));
```

### 3. Localize strings

```csharp
// Shows "Hello, World." if the key is not found
var text = Loc.Localize("Greeting", "Hello, World.");

// With format arguments (T() is a shorthand for Localize())
var greeting = Loc.T("HelloName", "Hello, {0}.", userName);
```

## API overview

| Method | Description |
| --- | --- |
| `Loc.Setup(string locData)` | Sets up localization data for the calling assembly from a YAML string |
| `Loc.Setup(string locData, Assembly assembly)` | Sets up localization data for the given assembly from a YAML string |
| `Loc.SetupFromFile(string path)` | Sets up localization data for the calling assembly from a YAML file |
| `Loc.SetupFromFile(string path, Assembly assembly)` | Sets up localization data for the given assembly from a YAML file |
| `Loc.SetupWithFallbacks()` / `Loc.SetupWithFallbacks(Assembly assembly)` | Sets up empty data so fallback strings are always shown |
| `Loc.Localize(string key, string fallBack)` | Retrieves the string for a key (or the fallback if not found) |
| `Loc.Localize(string key, string fallBack, params object[] args)` | Same as above, formatted with `string.Format`-style arguments |
| `Loc.T(string key, string fallBack)` / `Loc.T(string key, string fallBack, params object[] args)` | Shorthand aliases for `Localize()` |

Every method also has an overload that lets you explicitly specify the target assembly.

## Translation data schema

Each entry maps to the `LocEntry` type.

```csharp
public class LocEntry
{
    public string Message { get; set; }      // message: the actual translated text
    public string Description { get; set; }  // description: a note for translators
}
```

## License

Yamloc is provided under the [MIT License](..\LICENSE).

## Acknowledgements

This library was inspired by and built as a reference implementation of [goaaats/CheapLoc](https://github.com/goaaats/CheapLoc). Thanks to the CheapLoc author and contributors.
