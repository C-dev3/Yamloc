[![NuGet Version](https://img.shields.io/nuget/v/Yamloc)](https://www.nuget.org/packages/Yamloc/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yamloc)](https://www.nuget.org/packages/Yamloc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# Yamloc

A lightweight, YAML-based .NET localization library inspired by [CheapLoc](https://github.com/goaaats/CheapLoc).

[日本語版 README はこちら](./README.md)

## Repository layout

This repository is a single solution containing two NuGet packages.

```
Yamloc/
├── Yamloc.sln
├── LICENSE
├── README.md                  ← Japanese solution README
├── README.en.md                ← this file
│
├── Yamloc/                    ← run-time library
│   ├── Yamloc.csproj
│   ├── Loc.cs
│   ├── Entry.cs
│   └── README.md              ← NuGet package readme (bundled into the nupkg)
│
├── Yamloc.Export/             ← build-time export tooling
│   ├── Yamloc.Export.csproj
│   └── LocExporter.cs
│
└── Yamloc.Export.Tools/       ← cli for export tooling
    ├── Yamloc.Export.Tool.csproj
    └── Program.cs
```

| Package | Overview | Details |
|---|---|---|
| `Yamloc` | Run-time localization API. A lightweight standalone library depending only on YamlDotNet | [Yamloc/README.md](./Yamloc/README.md) |
| `Yamloc.Export` | Build-time tool that scans a compiled assembly's IL and generates a starter translation YAML file (depends on Mono.Cecil) | Documented within [Yamloc/README.md](./Yamloc/README.md) |

See the linked README above for detailed usage of each package (API reference, YAML file format, how to use the export tool, etc.). This README only covers how the repository as a whole is built and how the two packages relate to each other.

## Why the packages are split

`Yamloc.Export` uses Mono.Cecil for IL analysis, which is only needed at build time — it's not a dependency you want to carry into a run-time application (especially game plugins targeting AOT/trimming). For that reason, the run-time API (`Yamloc`) and the build-time tool (`Yamloc.Export`) are published as separate packages. `Yamloc.Export` has a project reference to `Yamloc`, so installing `Yamloc.Export` automatically pulls in `Yamloc` as a dependency as well.

## Requirements

- Any runtime that supports .NET Standard 2.0 (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+, etc.)

## Building

```bash
git clone https://github.com/C-dev3/Yamloc.git
cd Yamloc
dotnet build
```

Both projects have `GeneratePackageOnBuild` enabled, so `.nupkg` / `.snupkg` files are produced under `bin/Debug` (or `bin/Release`) with a plain `dotnet build`. To pack explicitly:

```bash
dotnet pack -c Release
```

## Referencing locally

If you've cloned the repo and want to test the packages locally, either add the generated `.nupkg` as a local NuGet feed, or reference the projects directly via `ProjectReference` from your own project.

## Contributing

Issues and pull requests are welcome, especially:

- Bug reports (reproduction steps are appreciated)
- Corrections or improvements to the documentation (including this README)
- Feature proposals (opening an issue first to discuss is preferred)

## License

MIT License. See [LICENSE](./LICENSE) at the repository root for details.
