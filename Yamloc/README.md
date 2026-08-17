# Yamloc

Yamloc は、YAML をベースとした軽量な .NET ローカライゼーション（多言語化）ライブラリです。

このライブラリは [CheapLoc](https://github.com/goaaats/CheapLoc) を参考にして作られました。CheapLoc の「導入が簡単で、専用のローカライゼーション用データベースを必要としない」というコンセプトを引き継ぎつつ、翻訳ファイルの形式として JSON の代わりに、人間が読み書きしやすい YAML を採用しています。
[English README is here](./README.en.md)

## 特徴

- **シンプルな API** — `Loc.Setup()` を呼び出すだけで導入可能
- **YAML ベース** — コメントを書きやすく、手編集にも向いたフォーマット
- **アセンブリ単位の管理** — 呼び出し元のアセンブリを自動的に判別し、アセンブリごとにローカライズデータを分離
- **フォールバック対応** — キーが見つからない場合は指定したフォールバック文字列を表示
- **フォーマット引数対応** — `string.Format` 互換の引数を使った文字列の組み立てが可能
- **翻訳者向けコンテキスト** — 各エントリに `description`（使用箇所の説明）を付与可能
- **スレッドセーフ** — `Setup()` によるデータの差し替えと `Localize()` の並行呼び出しに対応

なお、本パッケージ (`Yamloc`) には実行時のローカライズ用 API のみが含まれており、依存ライブラリは [YamlDotNet](https://github.com/aaubry/YamlDotNet) のみです。ビルド時に翻訳対象文字列を抽出するエクスポートツール（`ExportLocalizable` / `ExportLocalizableForAssembly` 相当の機能）は、[Mono.Cecil](https://github.com/jbevain/cecil) に依存する別パッケージ `Yamloc.Export` で提供される予定です。

## インストール

```bash
dotnet add package Yamloc
```

## 使い方

### 1. 翻訳データ（YAML）を用意する

`message` は必須、`description` は翻訳者向けの補足情報（任意）です。

```yaml
Greeting:
  message: "こんにちは、世界！"
  description: "アプリ起動時に表示されるあいさつ文"
HelloName:
  message: "こんにちは、{0} さん。"
  description: "ユーザー名を含むあいさつ（引数1つ）"
```

### 2. 初期化する

アプリ起動時などに `Loc.Setup()` / `Loc.SetupFromFile()` を呼び出します。

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
    // 該当する言語データがない場合は、フォールバック文字列のみを使用する
    Loc.SetupWithFallbacks();
}
```

YAML 文字列を直接渡すことも可能です。

```csharp
Loc.Setup(File.ReadAllText("loc_ja.yaml"));
```

### 3. 文字列をローカライズする

```csharp
// キーが見つからない場合は "Hello, World." が表示される
var text = Loc.Localize("Greeting", "Hello, World.");

// フォーマット引数付き（T() は Localize() のショートハンド）
var greeting = Loc.T("HelloName", "Hello, {0}.", userName);
```

## API 概要

| メソッド | 説明 |
| --- | --- |
| `Loc.Setup(string locData)` | 呼び出し元アセンブリ向けに、YAML 文字列からローカライズデータを設定する |
| `Loc.Setup(string locData, Assembly assembly)` | 指定したアセンブリ向けに、YAML 文字列からローカライズデータを設定する |
| `Loc.SetupFromFile(string path)` | 呼び出し元アセンブリ向けに、YAML ファイルからローカライズデータを設定する |
| `Loc.SetupFromFile(string path, Assembly assembly)` | 指定したアセンブリ向けに、YAML ファイルからローカライズデータを設定する |
| `Loc.SetupWithFallbacks()` / `Loc.SetupWithFallbacks(Assembly assembly)` | 空のデータを設定し、常にフォールバック文字列を表示させる |
| `Loc.Localize(string key, string fallBack)` | キーに対応する文字列を取得（見つからない場合はフォールバック） |
| `Loc.Localize(string key, string fallBack, params object[] args)` | 上記に加え、`string.Format` 互換の引数でフォーマットする |
| `Loc.T(string key, string fallBack)` / `Loc.T(string key, string fallBack, params object[] args)` | `Localize()` のショートハンドエイリアス |

いずれのメソッドにも、対象アセンブリを明示的に指定できるオーバーロードが用意されています。

## 翻訳データのスキーマ

各エントリは `LocEntry` 型に対応します。

```csharp
public class LocEntry
{
    public string Message { get; set; }     // message: 実際の翻訳文
    public string Description { get; set; }  // description: 翻訳者向けの補足説明
}
```

## ライセンス

Yamloc は [MIT License](..\LICENSE) の下で提供されます。

## 謝辞

本ライブラリは [goaaats/CheapLoc](https://github.com/goaaats/CheapLoc) を参考に作られました。CheapLoc の作者およびコントリビューターに感謝します。
