[![NuGet Version](https://img.shields.io/nuget/v/Yamloc)](https://www.nuget.org/packages/Yamloc/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Yamloc)](https://www.nuget.org/packages/Yamloc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

# Yamloc

[CheapLoc](https://github.com/goaaats/CheapLoc) にインスパイアされた、YAML ベースの軽量な .NET ローカライゼーションライブラリです。

[English README is here](./README.en.md)

## リポジトリ構成

このリポジトリは1つのソリューションに2つの NuGet パッケージを含んでいます。

```
Yamloc/
├── Yamloc.sln
├── LICENSE
├── README.md                  ← このファイル（ソリューション全体の説明）
├── README.en.md
│
├── Yamloc/                    ← 実行時ライブラリ本体
│   ├── Yamloc.csproj
│   ├── Loc.cs
│   ├── Entry.cs
│   └── README.md              ← NuGetパッケージ詳細（nupkgに同梱）
│
├── Yamloc.Export/             ← ビルド時エクスポートツール
│   ├── Yamloc.Export.csproj
│   └── LocExporter.cs
│
└── Yamloc.Export.Tools/       ← エクスポートツールCLI
    ├── Yamloc.Export.Tool.csproj
    └── Program.cs
```

| パッケージ | 概要 | 詳細 |
|---|---|---|
| `Yamloc` | 実行時のローカライズAPI。YamlDotNetのみに依存する軽量な単体ライブラリ | [Yamloc/README.md](./Yamloc/README.md) |
| `Yamloc.Export` | コンパイル済みアセンブリのILを走査し、翻訳用YAMLの雛形を自動生成するビルド時ツール（Mono.Cecil依存） | [Yamloc/README.md](./Yamloc/README.md) 内に記載 |

各パッケージの詳しい使い方（API一覧、YAMLファイルの書式、エクスポートツールの使い方など）は上記リンク先を参照してください。このREADMEでは、リポジトリ全体のビルド方法とパッケージ間の関係のみを扱います。

## なぜパッケージが分かれているか

`Yamloc.Export` は IL 解析に Mono.Cecil を使用しますが、これはビルド時にのみ必要で、実行時のアプリケーション（特に AOT/トリミング対象のゲームプラグインなど）には持ち込みたくない依存関係です。そのため、実行時 API（`Yamloc`）とビルド時ツール（`Yamloc.Export`）を別パッケージに分離しています。`Yamloc.Export` は `Yamloc` をプロジェクト参照しているため、`Yamloc.Export` を導入すれば `Yamloc` も自動的に依存関係として付いてきます。

## 動作要件

- .NET Standard 2.0 に対応する任意のランタイム（.NET Framework 4.6.1+ / .NET Core 2.0+ / .NET 5+ など）

## ビルド方法

```bash
git clone https://github.com/C-dev3/Yamloc.git
cd Yamloc
dotnet build
```

NuGetパッケージ（`.nupkg` / `.snupkg`）は各プロジェクトで `GeneratePackageOnBuild` が有効になっているため、`dotnet build` だけで `bin/Debug` (または `bin/Release`) 配下に生成されます。個別にパッケージ化したい場合は、

```bash
dotnet pack -c Release
```

## ローカルでの参照方法

このリポジトリを clone して手元でパッケージを検証したい場合は、`dotnet pack` で生成した `.nupkg` をローカルの NuGet フィードとして参照するか、あるいは自分のプロジェクトから直接プロジェクト参照 (`ProjectReference`) してください。

## コントリビュート

Issue・Pull Requestは歓迎します。特に以下は歓迎です。

- バグ報告（再現手順を添えていただけると助かります）
- ドキュメント（本README含む）の誤りの指摘・改善
- 新機能の提案（Issueでまず相談していただけるとスムーズです）

## ライセンス

MIT License。詳細はリポジトリルートの [LICENSE](./LICENSE) を参照してください。
