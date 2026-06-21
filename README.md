# DStockAnalysis

日本の上場銘柄を **情報・決算結果・財務数値・配当/株主優待・テーマ** で集約し、
「自分が買いたい銘柄」を見つけるための Windows デスクトップアプリ(投資研究ダッシュボード)です。

> このアプリは証券会社アプリではありません。売買注文・口座連携・自動売買・保有損益管理・板情報・
> デイトレード機能はありません。**買い/売りの推奨も行いません。**
> 情報・結果・数値から自分で判断するための補助と、使ううちに **バフェット型の投資思考が自然に身につく**
> 設計を目的としています。

---

## 技術構成

- Windows デスクトップ / **C# / WPF / .NET 8 (`net8.0-windows`)**
- **MVVM** 構成(Models / ViewModels / Views / Services を分離)
- ローカル保存(**JSON**、`%AppData%\DStockAnalysis`)
- **全銘柄を自動取得**: 日本取引所グループ(JPX)公開の「東証上場銘柄一覧(`data_j.xls`)」を同梱し、
  起動時に全上場銘柄(プライム/スタンダード/グロースの内国株式・約3,700銘柄)を自動ロード。
  一覧は毎月更新されるため、アプリ内の **全銘柄更新(JPX)** ボタンで最新版に差し替え可能。
- **CSV インポート**対応(列順不問・欠損列は空欄/0扱い。銘柄コードで照合し実データを上書き)
- 外部 API 連携は将来拡張前提(J-Quants / EDINET / TDnet / PDFキーワード検索を追加しやすい構造)

> **動作前提**: 本アプリは **ローカル単独動作**を想定しています。データはすべて自分の PC 内に保存されます。
> インターネット接続は、初回ビルド時の NuGet 取得と、JPX 銘柄一覧の月次更新(任意)時のみ使用します。
> 同梱の銘柄一覧があるため、オフラインでもそのまま全銘柄を表示・スクリーニングできます。

> **指標値について(重要)**: JPX の上場銘柄一覧には「コード・銘柄名・市場・業種・規模」しか含まれず、
> PER・ROE・配当などの財務指標は含まれません。本アプリは全銘柄に **もっともらしいサンプル(擬似)指標値**
> を自動生成して表示し、スクリーニングや比較をすぐ体験できるようにしています。実データに置き換える場合は
> **CSV取込**(`sample_stocks.csv` と同じ列名)を使ってください。擬似値の銘柄数は画面右下に表示されます。

## 動作要件

- Windows 10 / 11(64bit)
- .NET 8 SDK もしくはデスクトップランタイム
  - https://dotnet.microsoft.com/download/dotnet/8.0
- 推奨解像度: 1920×1080(最低 1366×768、最大化表示前提)

## ローカル環境構築手順(詳細)

### 手順 0: 必要なもの
- Windows 10/11(64bit)
- .NET 8(SDK もしくは Desktop Runtime)
- ソース一式(本フォルダ)

### 手順 1: .NET 8 をインストールする
1. ブラウザで https://dotnet.microsoft.com/download/dotnet/8.0 を開く。
2. 用途に合わせてダウンロード:
   - **ビルドも行う/開発する** → 「**.NET 8.0 SDK**」(x64) のインストーラー。
   - **実行するだけ** → 「**.NET Desktop Runtime 8.0**」(x64、"Run desktop apps" の方)。
3. ダウンロードした `.exe` を実行し、画面に従ってインストール。
4. インストール確認:スタートメニューから **PowerShell**(または コマンドプロンプト)を開き、
   ```
   dotnet --version
   ```
   を実行。`8.x.x`(SDK の場合)が表示されれば OK。
   - `'dotnet' は…認識されていません` と出る場合は、一度サインアウト/再起動して PATH を反映させる。

### 手順 2: ソースを配置する
- 本リポジトリを任意のフォルダ(例:`C:\apps\DStockAnalysis`)に **clone またはコピー**。
  ```
  git clone https://github.com/184649/DStockAnalysis.git
  ```
- フォルダ内に `DStockAnalysis.csproj` と `run.bat`、`Data\data_j.xls`(同梱の全銘柄一覧)があることを確認。

### 手順 3: 起動する(いずれか 1 つ)

**(A) バッチで起動 — 最も簡単**
1. エクスプローラーで `DStockAnalysis` フォルダを開く。
2. `run.bat` を **ダブルクリック**。
   - 初回はビルド(必要なら NuGet 取得)が走り、数十秒後にアプリが起動します。
   - .NET 未検出時は案内メッセージが出ます。

**(B) コマンドラインで起動**
1. フォルダ内で PowerShell を開く(アドレスバーに `powershell` と入力 → Enter)。
2. 次を実行:
   ```
   dotnet run --project DStockAnalysis.csproj -c Release
   ```

**(C) Visual Studio 2022 で起動**
1. 「.NET デスクトップ開発」ワークロードを入れた VS2022 を用意。
2. `DStockAnalysis.csproj`(または同フォルダ)を開く。
3. **F5**(デバッグ実行)または **Ctrl+F5**(デバッグなし実行)。

**(D) 配布用に単一フォルダを作る(.NET 未インストール PC でも動く)**
```
dotnet publish DStockAnalysis.csproj -c Release -r win-x64 --self-contained true -o publish
```
生成された `publish\DStockAnalysis.exe` をダブルクリックで起動(フォルダごとコピーすれば他 PC でも動作)。

### 手順 4: 起動後の確認
- ウィンドウが最大化で開き、**銘柄スクリーニング画面**に全銘柄(約3,700)が表示されれば成功です。
- 画面右上 **データ更新日**(JPX一覧の基準日)、右下に **全○○銘柄 / うち△△件は指標がサンプル値** が表示されます。

### うまくいかないとき
| 症状 | 対処 |
|---|---|
| `dotnet` が見つからない | .NET 8 を再インストール後、PC を再起動 |
| ビルドで NuGet 取得に失敗 | 一時的にネット接続して再実行(`NPOI` 等を取得) |
| 一覧が空/サンプル14件のみ | `Data\data_j.xls` が存在するか確認(同梱物)。または **全銘柄更新(JPX)** を押す |
| 文字が□になる | Windows に游ゴシック/メイリオがあるか確認(通常は標準搭載) |

## データの更新(JPX 月次)

JPX の上場銘柄一覧は **毎月更新**されます(月末営業日基準)。
- アプリ右上の **全銘柄更新(JPX)** を押すと、最新の `data_j.xls` をダウンロードして全銘柄を読み込み直します
  (ネット接続が必要。メモ・チェックはコードで引き継がれます)。
- データ更新日が古くなると、右上に **「(更新を推奨)」** と表示されます(35 日経過目安)。
- 取得元 URL:
  `https://www.jpx.co.jp/markets/statistics-equities/misc/tvdivq0000001vg2-att/data_j.xls`

## 使い方

### 1. データの考え方
- 起動した時点で **全上場銘柄が読み込まれています**(自分で銘柄を用意する必要はありません)。
- 各指標は初期状態では **サンプル(擬似)値** です。**ユーザーは各指標に絞り込み条件(最小〜最大)を
  設定して銘柄をフィルタリング**します。
- 実際の財務数値で分析したい場合は、`sample_stocks.csv` と同じ列名の CSV を用意し、画面右上の
  **CSV取込** で読み込みます(`Code` 列で既存銘柄に照合し、指標を実データへ上書き。未登録コードは追加)。

### 2. 銘柄スクリーニング(条件で絞り込む)
- 左パネルで業種・市場(東証/GR/PR/ST)・規模・配当還元フラグ・各種数値レンジ(最小〜最大)を指定し
  **絞り込み**。**クリア**で条件リセット。
- 上部の**プリセットボタン**(高配当で好財務 / 財務健全 / 高成長 / しけなぎ投資法 /
  株主優待あり高利回り など)を押すと、条件が自動入力され一覧が絞り込まれます。
- 一覧のセルは数値の良し悪しで色分け(青/緑=良、橙=注意、赤=悪)。フラグは ○ / - / ◎。
- 行を**ダブルクリック**、または **個別分析を開く / 比較に追加** で次の画面へ。

### 3. 個別銘柄分析(深く調べる)
- 左の銘柄リストから選択。指標カード・業績/配当/財務/CF チャート・スコアレーダーを確認。
- **バフェットチェック**(15 項目)を はい/いいえ/不明 で回答すると、バフェットスコアに反映されます。
- **メモ**(見つけた理由・良い点/悪い点・キオクシア候補理由・分類など)と**興味度**を入力し、
  **チェック・メモを保存してスコア再計算** で保存。

### 4. 銘柄比較(横並びで比べる)
- 左のリストから **選択銘柄を比較に追加**(最大 6 銘柄)。上部チップの ✕ や行の ✕ で削除。
- 中央の表で同じ指標を横比較、下部の比較グラフ(スコア/業績/財務/配当)で視覚比較。

### 5. 保存
- 画面右上の **保存** で、銘柄データ・メモ・チェック・比較対象・設定を保存します
  (保存先: `%AppData%\DStockAnalysis`)。次回起動時に自動で読み込まれます。

## テストの実行

```
dotnet test Tests/DStockAnalysis.Tests.csproj
```
単体テスト(Service/Model/Common)と結合テスト(画面横断フロー)を実行します。
詳細は `docs/単体試験項目書.md` / `docs/結合試験項目書.md` を参照してください。

## ドキュメント

| 種別 | ファイル |
|---|---|
| 基本設計書 | `docs/基本設計書.md` |
| 詳細設計書 | `docs/詳細設計書.md` |
| 単体試験項目書 | `docs/単体試験項目書.md` |
| 結合試験項目書 | `docs/結合試験項目書.md` |

---

## 画面構成

### 1. 銘柄スクリーニング画面
- 左: 条件指定パネル(業種/市場/規模、株主優待フィルタ、各種数値レンジ最小〜最大)
- 上部: プリセット条件ボタン(高配当で好財務 / 財務健全 / 高成長 / しげなさ投資法 /
  下落中の割安バリュー株 / 増配×成長性 / 優待あり高利回り など)
- 中央: 高密度の銘柄一覧テーブル(横スクロール、コード・銘柄名を左に固定、
  カテゴリ順に基本情報→バリュエーション→配当/還元→株主優待→財務→成長性→
  キャッシュフロー→株価変化→スコア)
- 数値は条件付き書式で色分け(良い=青/緑、注意=オレンジ、悪い=赤、中立=ダークグレー、
  フラグは ○ / - / ◎)

### 2. 個別銘柄分析画面
- 左: 銘柄リスト(検索付き)
- ヘッダー: コード/銘柄名/市場/業種/決算月/企業概要/IRリンク/データ更新日/画面切替
- 指標カード: 株価・時価総額・PER・PBR・ROE・MIX係数・自己資本比率・配当利回り・配当性向・
  総合評価・長期適性スコア・再評価期待スコア・**バフェットスコア(強調表示)**・買いたい度スコア
- グラフ: 業績推移 / 配当推移 / 財務推移 / キャッシュフロー推移 / 自己株式の取得(簡易バーチャート)
- スコアレーダー: 安全性・成長性・収益性・還元性・効率性・割安性
- **バフェットチェック**(15項目を はい/いいえ/不明 で管理 → スコアに反映)
- 株主優待・株主還元エリア(優待利回り/配当利回り/総合利回り/長期保有優遇/廃止リスク)
- メモ欄(見つけた理由・良い点/悪い点・キオクシア候補理由・次に確認する情報・分類)

### 3. 銘柄比較画面
- 左: 銘柄選択リスト、上部: 比較対象チップ
- 中央: 指標を縦に並べた比較テーブル(セルは条件付き書式)
- 下部: 比較グラフ(スコア比較 / 業績・規模 / 財務 / 配当・還元)

---

## スコア設計(すべて 0–100、買い/売りは出しません)

| スコア | 観点 |
|---|---|
| 安全性/成長性/収益性/還元性/効率性/割安性 | レーダーの6軸 |
| 長期適性スコア | 財務健全性・収益安定性・株主還元・CF・参入障壁・10年後需要 |
| 再評価期待スコア | 売上/営業利益/EPS成長・市場評価の変化・テーマ需要 |
| **バフェットスコア** | 事業理解/長期需要15・競争優位/参入障壁15・収益性15・キャッシュ創出力15・財務健全性15・株主還元の質10・割安性10・暴落時保有適性5(+バフェットチェック反映) |
| 買いたい度スコア | 長期適性・再評価・バフェット・割高感・自分の興味・事業理解・メモ分類 |
| 総合評価/総合判定 | 上記を統合(最重要候補/長期優良株候補/第二のキオクシア候補/再評価候補/高配当・還元候補/テーマ候補/決算確認候補/調査中/保留/除外) |

> **株主優待の扱い**: 優待は株主還元スコアの加点要素として扱いますが、過大評価しません。
> 業績不振なのに優待利回りが高い・配当性向が高すぎる・フリーCFが弱い場合は加点を抑制します。
> バフェットスコアでは優待そのものは小さくしか加点せず、事業の強さ・キャッシュ創出力・
> 還元の持続性・長期需要・価格の妥当性を優先します。

---

## CSV 取り込み

`Code` 列が必須です。ヘッダー名でマッピングするため列順は自由、未知/欠損列は空欄(0)扱いです。
対応列はサンプル `sample_stocks.csv` を参照してください。主な列:

```
Code, Name, Market, Sector, Scale, Theme, Description, Price, MarketCap,
PER, PBR, ROE, MixFactor, DividendYield, PayoutRatio, EquityRatio,
InterestBearingDebtRatio, ConsecutiveDividendYears, DividendCutCount,
NonDividendCutYears, RevenueGrowth1Y/3Y/5Y/10Y, FreeCashFlow / FreeCF,
OperatingCashFlowMargin, DividendRemainingYears, FiscalMonth, EPS, BPS,
NetProfitMargin, OperatingMargin, OrdinaryProfitMargin,
RevenueGrowthRate, AverageRevenueGrowth3Y, OperatingProfitGrowthRate,
OrdinaryProfitGrowthRate, NetProfitGrowthRate, EpsGrowthRate,
StockPriceChange3M, AverageStockPriceChange3M, PriceChange3M,
AveragePrice3M, PriceChangeAverage3M, IRUrl,
Dividend, DividendTrend, CumulativeDividend, DoeAdopted, BuybackAmount,
ShareholderReturnPolicy, OperatingCF, InvestingCF, FinancingCF,
HasShareholderBenefit, ShareholderBenefit, BenefitContent, BenefitCategory,
BenefitRightsMonth, RequiredSharesForBenefit, BenefitValue, BenefitYield,
TotalYield, HasLongTermBenefit, LongTermBenefitCondition,
LongTermBenefitContent, BenefitRiskMemo,
BuffettScore, SafetyScore, GrowthScore, ProfitabilityScore,
ReturnScore, EfficiencyScore, ValuationScore
```

CSV にスコア列が無くてもアプリ側で自動算出します(取り込み後に再計算)。

---

## ローカル保存

`%AppData%\DStockAnalysis` に以下を保存します。

- `stocks.json` … 取り込んだ銘柄データ
- `userdata.json` … 銘柄ごとのメモ・バフェットチェック・興味度
- `settings.json` … 比較対象・スクリーニング条件・アプリ設定

CSV を再取り込みしてもメモ/チェックは銘柄コードで再マージされます。

---

## プロジェクト構成

```
DStockAnalysis.csproj
app.manifest
App.xaml / App.xaml.cs           … リソース・VM→Viewマッピング・チャートテンプレート
MainWindow.xaml / .cs            … 上部ナビ・データ更新日・CSV取込/保存
Themes/DarkTheme.xaml            … ダーク(Power BI風)テーマ
Converters/Converters.cs         … 条件付き書式・フラグ・色変換
Common/ObservableObject.cs
Models/                          … Stock, TimeSeriesPoint, BuffettCheck, StockMemo,
                                    ScreeningCriteria, ScreeningPreset, AppData, Enums
Services/                        … CsvImportService, DataStorageService, ScoreService,
                                    SampleDataService, PresetService
ViewModels/                      … Main, Screening, StockAnalysis, Comparison,
                                    ChartModels, RelayCommand, ViewModelBase
Views/                           … ScreeningView, StockAnalysisView, ComparisonView
sample_stocks.csv
run.bat
```

## 将来拡張の指針

- グラフは `ViewModels/ChartModels.cs` の `ChartGroup/ChartSeries/ChartBar` に依存しているため、
  描画部(`App.xaml` の `ChartGroup` テンプレート)を **LiveCharts2** などに差し替え可能。
- データ取得は `Services` に新規サービス(`JQuantsService` 等)を追加し、
  `CsvImportService` と同様に `List<Stock>` を返す形にすれば `MainViewModel` から差し込めます。
