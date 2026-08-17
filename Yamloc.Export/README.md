# Yamloc

[CheapLoc](https://github.com/goatcorp/Dalamud.CheapLoc) にインスパイアされた、YAML ベースの軽量な .NET ローカライゼーションライブラリです。JSON ではなく人間が編集しやすい YAML を採用しており、翻訳ファイルにコメントで文脈を残せます。

[English README is here](./README.en.md)

## パッケージ構成

このリポジトリは2つの NuGet パッケージで構成されています。

| パッケージ | 役割 | 依存関係 |
|---|---|---|
| [`Yamloc`](#yamloc-1) | 実行時のローカライズ API（`Setup` / `Localize` / `T`） | YamlDotNet のみ |
| [`Yamloc.Export`](#yamlocexport) | ビルド時のツール。コンパイル済みアセンブリの IL を走査し、YAML ファイルの雛形を自動生成 | YamlDotNet, Mono.Cecil, `Yamloc` |

実行時に文字列を差し込むだけであれば `Yamloc` だけで十分です。`Yamloc.Export` はソースコード中の `Loc.T(...)` 呼び出しを自動で収集して翻訳用 YAML の雛形を作りたい場合にのみ必要になります。Mono.Cecil に依存するため、実行時の成果物には含めないことを推奨します（AOT/トリミング環境との相性の都合上、あえてパッケージを分離しています）。

## Yamloc

### インストール

```
dotnet add package Yamloc
```

### 基本的な使い方

**1. 起動時にローカライズデータを読み込む**

```csharp
using Yamloc;

// ファイルから読み込む
Loc.SetupFromFile("path/to/ja.yml");

// または YAML 文字列を直接渡す（埋め込みリソースなど）
Loc.Setup(yamlString);
```

`Setup` / `SetupFromFile` は `Assembly.GetCallingAssembly()` で呼び出し元のアセンブリを自動判定し、アセンブリ単位で独立した名前空間に翻訳データを保持します。複数の DLL（プラグインなど）が同一プロセスに常駐していても、キーが衝突する心配はありません。

**2. 文字列をローカライズする**

```csharp
using Yamloc;

// key が見つからなければ fallback がそのまま表示される
string label = Loc.T("menu.save", "Save");

// string.Format 互換の引数付き呼び出しも可能
string greeting = Loc.T("greeting", "Hello, {0}!", playerName);
```

`fallback` にはソース言語（開発時の基準言語）の文字列をそのまま書きます。翻訳データが未整備の段階でもこの文字列がそのまま表示されるため、翻訳ファイルの完成を待たずに実装を進められます。

`Setup` を一度も呼んでいないアセンブリから呼び出した場合は `#key` という形式で返り、ローカライズが未設定であることが画面上で分かるようになっています。

### YAML ファイルの形式

```yaml
menu.save:
  message: "保存"
  description: "メインメニューの保存ボタン"

greeting:
  message: "こんにちは、{0}さん"
  description: "{0}=プレイヤー名"
```

各キーは `message`（実際に表示される文字列）と `description`（翻訳者向けの文脈説明）を持ちます。

翻訳データの一部（あるキー）が壊れていても、パースに成功した他のキーは正しく読み込まれます。壊れているキーだけがフォールバックにより代替されるため、翻訳ファイルの一箇所の不備がアプリ全体のローカライズを無効化することはありません。

### API 一覧

| メソッド | 説明 |
|---|---|
| `Loc.Setup(string locData)` | 呼び出し元アセンブリ向けに YAML 文字列からローカライズデータを設定 |
| `Loc.Setup(string locData, Assembly assembly)` | 指定アセンブリ向けに YAML 文字列からローカライズデータを設定 |
| `Loc.SetupFromFile(string path)` | 呼び出し元アセンブリ向けにファイルから YAML を読み込んで設定 |
| `Loc.SetupFromFile(string path, Assembly assembly)` | 指定アセンブリ向けにファイルから YAML を読み込んで設定 |
| `Loc.SetupWithFallbacks()` / `Loc.SetupWithFallbacks(Assembly)` | 空のローカライズデータを設定し、すべて fallback 文字列を表示させる |
| `Loc.Localize(string key, string fallBack)` / `Loc.T(string key, string fallBack)` | 文字列をローカライズして取得（`T` は `Localize` のエイリアス） |
| `Loc.Localize(string key, string fallBack, params object[] args)` / `Loc.T(string key, string fallBack, params object[] args)` | `string.Format` 形式の引数付きでローカライズ |

## Yamloc.Export

ソースコード中の `Loc.Localize(...)` / `Loc.T(...)` 呼び出しをコンパイル済みアセンブリの IL から静的に収集し、翻訳用 YAML ファイルの雛形を自動生成するビルド時ツールです。Mono.Cecil を使って IL を解析するため、実行時には不要です。

### インストール

```
dotnet add package Yamloc.Export
```

### 使い方

ビルド後のアセンブリに対して実行します（ビルドスクリプトや別の CLI プロジェクトなどから呼び出す想定です）。

```csharp
using Yamloc.Export;
using System.Reflection;

var assembly = Assembly.LoadFrom("path/to/YourPlugin.dll");
LocExporter.ExportLocalizableForAssembly(assembly);
```

呼び出し元アセンブリを自動判定する簡易版もあります。

```csharp
LocExporter.ExportLocalizable();
```

実行すると、コード中のすべての `Loc.T(key, fallback, ...)` / `Loc.Localize(key, fallback, ...)` 呼び出しを収集し、`{アセンブリ名}_Localizable.yaml` として出力します。同時に `loc.log` に、どのメソッドからどのキーが検出されたかのデバッグ情報が出力されます。

### 既存の翻訳を維持したまま再エクスポートする

`existingYamlPath` に既存の翻訳済み YAML ファイルを指定すると、差分マージが行われます。

```csharp
LocExporter.ExportLocalizableForAssembly(
    assembly,
    existingYamlPath: "ja.yml");
```

このとき、

- コード中に既に存在するキーで、かつ既存ファイルに翻訳済みの `message` があれば、その翻訳を維持したまま `description` だけ最新のコード上の位置に更新します
- コードから削除されたキーは出力から除外されます
- 新規に追加されたキーは、ソースコード上の `fallback` 文字列がそのまま `message` として出力されます（未翻訳の目印になります）

このため、実装が進んでコード中の呼び出し位置やキーが変わっても、翻訳者の作業済みの翻訳文言を失うことなく YAML ファイルを更新できます。

### 制約・注意点

- 同一キーに対して、呼び出し箇所ごとに異なる `fallback` 文字列を指定すると、エクスポート時に例外がスローされます（どちらが正なのか判断できないため）。同じキーは常に同じ fallback を使ってください。
- キーとして空文字列や `null` を渡す呼び出しはデフォルトで例外になります。`ignoreInvalidFunctions: true` を指定すると、該当箇所をスキップしてエクスポートを継続できます。
- 引数付き呼び出し（`Loc.T(key, fallback, args)`）にも対応していますが、これは IL 上のパターン（配列生成命令列）を検出して解析しているため、`fallback` と `key` には必ずコンパイル時定数の文字列リテラルを渡してください。動的に組み立てた文字列は検出できません。

## ライセンス

MIT License
