# DStockAnalysis（投資銘柄探索ダッシュボード／Webアプリ）

日本の上場銘柄を **株価・各種指標・配当・株主優待・スコア** で集約し、「自分が買いたい銘柄」を
見つけるための **Webアプリ**(ブラウザで使う投資研究ダッシュボード)です。

> 証券会社アプリではありません。売買注文・口座連携・自動売買・損益管理・板情報はありません。
> **買い/売りの推奨も行いません。** 情報・数値から自分で判断するための補助ツールです。

- 技術: **C# / .NET 8 / ASP.NET Core**（バックエンド）＋ 静的HTML/JavaScript（フロントエンド）
- 実行環境: **Windows / macOS / Linux**（ローカル）、**ConoHa VPS など Linux サーバ**（公開運用）
- 全上場銘柄(約3,700)を JPX 公開データから自動ロードし、**実データ(株価・指標)を自動取得**します

---

## 目次

1. [主な機能](#主な機能)
2. [データについて(重要)](#データについて重要)
3. [必要なもの](#必要なもの)
4. [かんたん起動(Windows・初心者向け)](#かんたん起動windows初心者向け)
5. [コマンドで起動(Windows / macOS / Linux)](#コマンドで起動windows--macos--linux)
6. [ConoHa VPS で公開する(初心者向け)](#conoha-vps-で公開する初心者向け)
7. [使い方](#使い方)
8. [自動取得の設定](#自動取得の設定)
9. [CSV 取り込み](#csv-取り込み)
10. [スコア設計](#スコア設計)
11. [プロジェクト構成](#プロジェクト構成)
12. [テスト](#テスト)
13. [困ったとき(FAQ)](#困ったときfaq)

---

## 主な機能

ブラウザで 3 つの画面を切り替えて使います。

- **銘柄スクリーニング**: 市場/業種/規模、各種数値レンジ、株主優待フラグで全銘柄を絞り込み。プリセット条件あり。条件付き書式で色分け。
- **個別銘柄分析**: 指標カード・スコアレーダー・チャート・バフェットチェック(15項目)・メモ。外部サイトへのリンク。
- **銘柄比較**: 最大6銘柄を横並びで比較。
- **実データの自動取得**: 銘柄を開くとその場で実値を取得。サーバ常駐で全銘柄の株価・指標も順次更新。
- **CSV 取り込み / テンプレ出力**: 自分で調べた実数値(株主優待など)を反映できます。

---

## データについて(重要)

JPX(日本取引所)の公開「上場銘柄一覧」には **コード・銘柄名・市場・業種・規模** しか含まれません。
本アプリは **擬似(サンプル)値を使わず**、未取得の銘柄は空欄(「-」)で表示し、次の出典から **実データ**を取得します。

| 指標 | 出典 |
|---|---|
| 株価(現在値)・ROE・純利益率・営業CF/フリーCF・有利子負債比率 | Yahoo! ファイナンス(構造化データ) |
| PER・PBR・配当利回り・時価総額(**会社予想ベース**=日本の投資情報サイトの表示に一致) | 株探 |
| 自己資本比率 | IR BANK |
| EPS・BPS・1株配当・配当性向・MIX係数 | 上記から算出 |
| 株主優待 | 自動取得の対象外。**CSV取込**で反映(自動取得では誤検出が避けられないため) |

- 株価は **銘柄を開くたび＋定期(既定6時間)** に最新化し、古い株価や株式分割前の価格が残らないようにしています。
- 取得は対象サイトの **robots.txt を順守**し、十分な間隔を空けて低頻度で行います(短期売買用ではありません)。
- 営業利益率・増収率などは無料で正確に取得できる出典が無いため、**未取得(「-」)** のままにします(誤った値を出さない方針)。

---

## 必要なもの

- **.NET 8 SDK**(無料)… これだけで動きます。
  - ダウンロード: https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0
- インターネット接続(実データ取得・初回ビルド時のNuGet取得に必要)
- ブラウザ(Chrome / Edge / Firefox / Safari など)

> Node.js や Python は不要です(フロントエンドはビルド不要の静的ファイル)。

---

## かんたん起動(Windows・初心者向け)

### 手順1: .NET 8 SDK をインストールする
1. ブラウザで https://dotnet.microsoft.com/ja-jp/download/dotnet/8.0 を開きます。
2. 「**.NET 8.0 SDK**」の **Windows x64** のインストーラー(`.exe`)をダウンロードします。
3. ダウンロードした `.exe` をダブルクリックし、画面の指示どおり「次へ」で進めてインストールします。
4. インストール確認: スタートメニューで「**PowerShell**」を開き、次を入力して Enter:
   ```
   dotnet --version
   ```
   `8.0.xxx` のように表示されれば成功です。
   - もし「'dotnet' は…認識されていません」と出たら、一度サインアウト/再起動してから再度お試しください。

### 手順2: ソースコードを入手する
- **方法A(おすすめ・簡単): ZIPでダウンロード**
  1. GitHub のページ(`https://github.com/184649/DStockAnalysis`)を開きます。
  2. 緑色の「**Code**」ボタン →「**Download ZIP**」をクリック。
  3. ダウンロードした ZIP を、**書き込みできる場所**(例: `ドキュメント`)に展開(右クリック →「すべて展開」)。
     - ※ `C:\Program Files` などの場所はビルドに失敗するので避けてください。
- **方法B: git で取得**(git を使える方)
  ```
  git clone https://github.com/184649/DStockAnalysis.git
  ```

### 手順3: 起動する
1. 展開した `DStockAnalysis` フォルダを開きます。
2. **`run-web.bat` をダブルクリック**します。
3. 黒い画面(コマンドプロンプト)が開き、初回はビルドが走ります(数十秒〜数分)。
4. しばらくすると、**ブラウザが自動で開き** `http://localhost:5000` にダッシュボードが表示されます。
   - 自動で開かない場合は、ブラウザのアドレス欄に手入力で `http://localhost:5000` と入れて Enter。
5. 終了するときは、黒い画面で **Ctrl+C** を押すか、画面を閉じます。

### 手順4: 動作確認
- 一覧に全銘柄(約3,700)が表示されます(指標は最初「-」=未取得)。
- 例えば検索欄に「8001」と入れて **伊藤忠商事** を開くと、実データ(株価・PER・PBR・配当利回り 等)が表示されます。

---

## コマンドで起動(Windows / macOS / Linux)

`run-web.bat` を使わず、コマンドで起動する方法です(全OS共通)。

1. ターミナル(Windowsは PowerShell)で、ソースの **リポジトリ直下**に移動します。
2. 次を実行:
   ```
   dotnet run --project DStockAnalysis.Web
   ```
3. ブラウザで表示されたURL(既定 `http://localhost:5000`)を開きます。
4. ポートを変えたい場合:
   ```
   dotnet run --project DStockAnalysis.Web --urls http://localhost:8080
   ```

> 既定(Development)では、銘柄を開いた時のオンデマンド取得と株価の定期更新が有効で、全銘柄の巡回取得は無効です。
> 全銘柄をまとめて取得したい場合は `-- --Fetch:Enabled=true` を付けて実行します(対象サイトへ低頻度アクセス)。

---

## ConoHa VPS で公開する(初心者向け)

ConoHa VPS(Ubuntu 22.04/24.04 LTS、メモリ 1GB 以上推奨)に常駐させ、ブラウザから使えるように公開する手順です。

### 手順0: VPSの準備
- ConoHa のコントロールパネルで Ubuntu のサーバーを作成し、SSH でログインします。
- 以下は一般ユーザー(例 `deploy`)で作業し、必要時のみ `sudo` を使います。

### 手順1: .NET 8 SDK を入れる
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --info   # 8.x が表示されればOK
```
> パッケージが見つからない場合は Microsoft の手順で `packages-microsoft-prod.deb` を追加してから上記を実行します。

### 手順2: ソース取得とビルド(発行)
```bash
sudo mkdir -p /var/www && sudo chown $USER /var/www
cd /var/www
git clone https://github.com/184649/DStockAnalysis.git
cd DStockAnalysis
dotnet publish DStockAnalysis.Web -c Release -o /var/www/dstock
```
- 同梱の `Data/data_j.xls` も発行先へコピーされ、初回起動で全銘柄を読み込みます。
- データ保存先を固定したい場合は `appsettings.Production.json` の `"DataDir"` を絶対パス(例 `/var/lib/dstock`)にし、そのフォルダを作成・書込可能にします。

### 手順3: サービス化(常駐＋自動再起動)
`/etc/systemd/system/dstock.service` を作成:
```ini
[Unit]
Description=DStockAnalysis Web
After=network.target

[Service]
WorkingDirectory=/var/www/dstock
ExecStart=/usr/bin/dotnet /var/www/dstock/DStockAnalysis.Web.dll
Restart=always
RestartSec=10
User=deploy
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5080

[Install]
WantedBy=multi-user.target
```
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now dstock
sudo systemctl status dstock          # Active(running) を確認
curl http://127.0.0.1:5080/api/meta    # 全銘柄数が返れば成功
```
> 本番(Production)では、全銘柄の巡回取得(財務指標)と株価の定期更新が既定で有効です。robots.txt 順守・低頻度です。

### 手順4: nginx で 80番ポート公開
```bash
sudo apt-get install -y nginx
```
`/etc/nginx/sites-available/dstock` を作成:
```nginx
server {
    listen 80;
    server_name _;   # ドメインがあれば記入
    location / {
        proxy_pass         http://127.0.0.1:5080;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```
```bash
sudo ln -s /etc/nginx/sites-available/dstock /etc/nginx/sites-enabled/dstock
sudo nginx -t && sudo systemctl reload nginx
```

### 手順5: ファイアウォール / HTTPS(任意)
```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx HTTP'
sudo ufw enable
```
- ConoHa の **セキュリティグループ**でも 80(/443)番の許可が必要です。
- 独自ドメインがあれば HTTPS 化を推奨:
  ```bash
  sudo apt-get install -y certbot python3-certbot-nginx
  sudo certbot --nginx -d your-domain.example
  ```
- 本アプリに認証機能はありません。公開範囲を限定したい場合は nginx の Basic 認証や ConoHa セキュリティグループで送信元IPを制限してください。

### 手順6: 更新(再デプロイ)
```bash
cd /var/www/DStockAnalysis && git pull
dotnet publish DStockAnalysis.Web -c Release -o /var/www/dstock
sudo systemctl restart dstock
```

---

## 使い方

画面上部のボタンで 3 画面を切り替えます。右上に **CSV取込 / テンプレ出力 / 全銘柄更新(JPX) / 実データ自動取得** があります。

### 1. 銘柄スクリーニング
- 左パネルで業種・市場・規模・株主優待フラグ・各種数値レンジ(最小〜最大)を指定して絞り込み。
- 上部の**プリセットボタン**(高配当で好財務 / 財務健全 / 高成長 / 株主優待あり高利回り など)で条件を自動入力。
- セルは数値の良し悪しで色分け。行を **ダブルクリック**で個別分析、**右クリック**で比較に追加。
- 指標が「-」の銘柄は未取得です。個別分析で開くと取得されます(全体はサーバの巡回取得で順次更新)。

### 2. 個別銘柄分析
- 左の一覧から銘柄を選択(または一覧からダブルクリック)。開くと実データを取得して表示します。
- 指標カード・スコアレーダー(6軸)・チャート・**バフェットチェック(15項目)**・メモを表示。
- 「**実データ更新**」ボタンで最新の実値を再取得できます。
- 株主優待は「未取得」と表示されます(CSV取込で反映)。

### 3. 銘柄比較
- 一覧から最大6銘柄を追加し、指標を横並びで比較します。

### CSV取込 / テンプレ出力
- **テンプレ出力**: 全銘柄の基本情報を前埋めした記入用CSVをダウンロード。
- 株主優待など自分で調べた数値を記入し、**CSV取込**で読み込むと、記入した列だけが実データとして反映されます。

---

## 自動取得の設定

`appsettings.json`(本番は `appsettings.Production.json`)の `Fetch` セクションで制御します。

| キー | 既定 | 説明 |
|---|---|---|
| `Enabled` | 本番 `true` | バックグラウンドの全銘柄巡回取得(財務指標)の有効/無効 |
| `OnDemand` | `true` | 個別分析で銘柄を開いた時に、その銘柄の実値をその場で取得する |
| `PriceRefresh` | `true` | 取得済み全銘柄の株価を定期一括最新化する(古い/分割前価格を防ぐ) |
| `PriceRefreshHours` | `6` | 株価一括最新化の間隔(時間) |
| `Scope` | `"all"` | `all`=全銘柄 / `watchlist`=`DataDir/codes.txt` の銘柄のみ |
| `DelaySeconds` | `8` | リクエスト間隔の基準秒(robots の Crawl-delay と大きい方を採用) |
| `MaxAgeDays` | `6` | この日数以内に取得済みの銘柄は再取得しない |

- 手動で株価を一括更新: `POST /api/admin/price-refresh`
- 取得状況の確認: `GET /api/admin/fetch/status`

---

## CSV 取り込み

`Code` 列が必須です。ヘッダー名でマッピングするため列順は自由、未知/欠損列は空欄(0)扱いです。
取り込んだ列だけが実データで上書きされます(列単位マージ)。主な列:

```
Code, Name, Market, Sector, Scale, Theme, Description, Price, MarketCap, PER, PBR, ROE,
DividendYield, PayoutRatio, EquityRatio, EPS, BPS, OperatingMargin, NetProfitMargin,
RevenueGrowth3Y, OperatingCF, FreeCashFlow,
HasShareholderBenefit, ShareholderBenefit, BenefitContent, BenefitCategory,
BenefitRightsMonth, RequiredSharesForBenefit, BenefitValue, BenefitYield, TotalYield,
HasLongTermBenefit, LongTermBenefitCondition, BenefitRiskMemo
```
列名サンプルは `sample_stocks.csv` を参照してください。**株主優待は CSV で反映**するのがおすすめです。

---

## スコア設計（すべて 0–100、買い/売りは出しません）

| スコア | 観点 |
|---|---|
| 安全性/成長性/収益性/還元性/効率性/割安性 | レーダーの6軸 |
| 長期適性スコア | 財務健全性・収益安定性・株主還元・CF・参入障壁・10年後需要 |
| 再評価期待スコア | 売上/営業利益/EPS成長・市場評価の変化・テーマ需要 |
| バフェットスコア | 事業理解・競争優位・収益性・キャッシュ創出力・財務健全性・還元の質・割安性・暴落耐性(＋バフェットチェック反映) |
| 買いたい度スコア | 長期適性・再評価・バフェット・割安感・興味・分類 |
| 総合評価 | 上記を統合し S/A/B/C/D とカテゴリ判定 |

> 未取得の指標がある銘柄はスコアが低め/未算出になります。実データが揃うほど精度が上がります。

---

## プロジェクト構成

```
DStockAnalysis.sln                 … ソリューション

DStockAnalysis.Core/               … 共有ドメイン(net8.0)
  Common/                          … ObservableObject, Grades
  Models/                          … Stock, ScreeningCriteria, BuffettCheck, StockMemo ほか
  Services/                        … ScoreService, CsvImportService, DataStorageService,
                                      JpxMasterService(NPOI), PresetService, ReferenceLinkService ほか

DStockAnalysis.Web/                … Web アプリ(ASP.NET Core)
  Program.cs                       … 最小API・DI・静的配信
  Services/StockStore.cs           … サーバ側の銘柄ストア
  Services/YahooFinanceClient.cs   … Yahoo!(株価・財務)取得
  Services/IndicatorFetchService.cs… 株探/IR BANK 取得(robots順守)
  Services/FetchCoordinator.cs     … 取得の一元管理・キャッシュ
  Services/IndicatorFetchHostedService.cs … 全銘柄巡回取得(常駐)
  Services/PriceRefreshHostedService.cs   … 株価の一括最新化(常駐)
  wwwroot/                         … index.html, css/, js/(3画面のフロントエンド)
  appsettings*.json
  Data/data_j.xls(リンク) / sample_stocks.csv(リンク)

Tests/                             … Core の単体・結合テスト(xUnit)
Tests.Web/                         … Web の単体・結合テスト(xUnit)
docs/                              … 設計書・試験項目書・データ取得手順
run-web.bat                        … Windows 用かんたん起動
```

---

## テスト

```
dotnet test DStockAnalysis.sln
```
- `Tests/`(Core): 単体(Service/Model/Common)＋結合(サンプル生成・スコア)= 69 件
- `Tests.Web/`(Web): 単体(指標解析)＋結合(StockStore・HTTP API・取得)= 36 件
- 合計 **105 件・0 失敗**。詳細は `docs/単体試験項目書.md` / `docs/結合試験項目書.md`。

---

## 困ったとき(FAQ)

| 症状 | 対処 |
|---|---|
| `dotnet` が見つからない | .NET 8 SDK を入れて、PCを再起動してから再実行 |
| `run-web.bat` がすぐ閉じる/ビルド失敗 | `C:\Program Files` 等の書込不可の場所を避け、ドキュメント等にコピーして実行 |
| ブラウザに何も出ない | 黒い画面に「Now listening on...」が出るまで待ってから `http://localhost:5000` を開く/再読み込み |
| ポートが使用中 | `--urls http://localhost:8080` で別ポート起動、または使用中のプロセスを終了 |
| 指標が「-」のまま | その銘柄をまだ取得していません。個別分析で開く/「実データ更新」を押す/巡回取得を待つ |
| 株主優待が出ない | 株主優待は自動取得対象外です。CSV取込で反映してください |
| 値が実際と少し違う | 各サイトの基準(会社予想/実績)・更新タイミングによる差です(ベストエフォート) |

---

## ドキュメント

| 種別 | ファイル |
|---|---|
| データ取得手順 | `docs/データ取得手順.md` |
| 基本設計書 | `docs/基本設計書.md` |
| 詳細設計書 | `docs/詳細設計書.md` |
| 単体試験項目書 | `docs/単体試験項目書.md` |
| 結合試験項目書 | `docs/結合試験項目書.md` |
