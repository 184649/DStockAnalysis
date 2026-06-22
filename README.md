# DStockAnalysis

日本の上場銘柄を **情報・決算結果・財務数値・配当/株主優待・テーマ** で集約し、
「自分が買いたい銘柄」を見つけるための Windows デスクトップアプリ(投資研究ダッシュボード)です。

> このアプリは証券会社アプリではありません。売買注文・口座連携・自動売買・保有損益管理・板情報・
> デイトレード機能はありません。**買い/売りの推奨も行いません。**
> 情報・結果・数値から自分で判断するための補助と、使ううちに **バフェット型の投資思考が自然に身につく**
> 設計を目的としています。

---

## 技術構成

- **2 エディション**: Windows デスクトップ(**WPF**)と **Web**(**ASP.NET Core**)。共通ロジックは
  `DStockAnalysis.Core`(`net8.0`)に集約し両方が参照(詳細は [エディション構成](#エディション構成デスクトップ--web))。
- C# / **.NET 8** / **MVVM**(デスクトップは Models / ViewModels / Views / Services 分離)
- Web 版は ASP.NET Core 最小API + 静的フロント(SPA)。Linux(ConoHa VPS)で常駐可能。
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

---

## エディション構成(デスクトップ / Web)

本アプリは **同一のドメインロジックを共有する 2 つのフロントエンド**を持ちます。
スコア計算・擬似指標生成・JPX 一覧解析・CSV 列単位マージなどの中核ロジックは
`DStockAnalysis.Core`(`net8.0` クラスライブラリ)に集約し、両エディションが参照します。

| プロジェクト | 役割 | 実行環境 |
|---|---|---|
| `DStockAnalysis.Core` | 共有ドメイン(Models/Services/Common) | net8.0(OS非依存) |
| `DStockAnalysis`(WPF) | デスクトップ版 UI | Windows |
| `DStockAnalysis.Web` | **Web 版**(ASP.NET Core API + 静的フロント) | Windows / **Linux(ConoHa VPS)** |
| `Tests` / `Tests.Web` | xUnit 試験 | net8.0 |

> **Web 版の特徴**
> - ブラウザから 3 画面(スクリーニング/個別分析/比較)をすべて利用可能。条件付き書式・スコアレーダー・
>   バフェットチェック・チャート・CSV取込・テンプレ出力・JPX更新に対応。
> - サーバ常駐の **自動取得バックグラウンドサービス**が、robots.txt を順守し低頻度で **全銘柄の主要指標**
>   (PER/PBR/ROE/配当利回り/時価総額/EPS 等)を巡回取得し、列単位マージで反映します
>   (`tools/fetch_data.py` の C# 移植)。`appsettings` で有効/無効・間隔・対象範囲を制御できます。
> - データは **ローカル単独の WPF 版とは別に、サーバ側の `DataDir`**(既定 `DStockAnalysis.Web/data`)へ
>   JSON 保存されます。

Web 版の起動・デプロイ手順は後述の **[Web 版ローカル起動](#web-版ローカル起動)** /
**[ConoHa VPS デプロイ](#conoha-vps-へのデプロイ)** を参照してください。

---

## 動作要件

### デスクトップ版(WPF)
- Windows 10 / 11(64bit)
- .NET 8 SDK もしくはデスクトップランタイム
  - https://dotnet.microsoft.com/download/dotnet/8.0
- 推奨解像度: 1920×1080(最低 1366×768、最大化表示前提)

### Web 版
- サーバ: Linux(Ubuntu 22.04/24.04 LTS、ConoHa VPS を想定)または Windows
- .NET 8 SDK(ビルド)/ ASP.NET Core 8 ランタイム(実行のみ)
- クライアント: 最新ブラウザ(Chrome/Edge/Firefox/Safari)

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
| **run.bat がすぐ閉じる/何も起きない** | 多くは `C:\Program Files` 配下に置いたことによる**ビルド時の書き込み権限不足**が原因。フォルダごと **ドキュメント等の書き込み可能な場所にコピー**して再実行してください。`run.bat` はビルド失敗時に既存ビルドでの起動を試み、原因をウィンドウに表示して `pause` で待機します(自動では閉じません)。 |

## Web 版ローカル起動

開発・動作確認用にローカルで Web 版を起動する手順です(Windows / macOS / Linux 共通)。

1. .NET 8 SDK を導入(`dotnet --version` で 8.x を確認)。
2. リポジトリ直下で次を実行:
   ```
   dotnet run --project DStockAnalysis.Web
   ```
   - 既定で `http://localhost:5000` で待ち受けます。ポートを変えるには:
     ```
     dotnet run --project DStockAnalysis.Web --urls http://localhost:5080
     ```
   - 初回起動時、同梱 `Data/data_j.xls` から全上場銘柄(約3,700)を読み込みます。
3. ブラウザで `http://localhost:5000`(または指定ポート)を開く。
4. 自動取得(スクレイピング)は **ローカル(Development)では既定で無効**です。試したい場合のみ:
   ```
   dotnet run --project DStockAnalysis.Web --urls http://localhost:5080 -- --Fetch:Enabled=true
   ```
   ※ 対象サイトへ低頻度(既定8秒間隔)でアクセスします。robots.txt を順守します。

> データ(銘柄・メモ・取得キャッシュ)は `DStockAnalysis.Web/data` 配下の JSON に保存され、`.gitignore` 済みです。

---

## ConoHa VPS へのデプロイ

ConoHa VPS(Ubuntu 22.04/24.04 LTS、メモリ 1GB 以上推奨)に Web 版を常駐させ、
**全銘柄の主要指標を自動取得**しながら社内/個人用ダッシュボードとして公開する手順です。

### 手順 0: VPS の準備
- ConoHa コントロールパネルで Ubuntu のサーバーを作成。SSH 鍵を登録してログイン。
- 一般ユーザー(例 `deploy`)で作業し、必要時のみ `sudo` を使う想定。

### 手順 1: .NET 8 SDK の導入
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
dotnet --info   # 8.x を確認
```
> パッケージが見つからない場合は Microsoft の手順に従い `packages-microsoft-prod.deb` を追加してから上記を実行します。

### 手順 2: ソース取得とビルド(publish)
```bash
sudo mkdir -p /var/www && sudo chown $USER /var/www
cd /var/www
git clone https://github.com/184649/DStockAnalysis.git
cd DStockAnalysis
dotnet publish DStockAnalysis.Web -c Release -o /var/www/dstock
```
- 同梱の `Data/data_j.xls` も発行先へコピーされ、初回起動で全銘柄を読み込みます。
- データ保存先を固定したい場合は `appsettings.Production.json` の `"DataDir"` を絶対パス
  (例 `/var/lib/dstock`)に設定し、そのディレクトリを作成・書込可能にします。

### 手順 3: systemd サービス化(常駐 + 自動再起動)
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
# 自動取得を止める場合は次行を有効化: Environment=Fetch__Enabled=false

[Install]
WantedBy=multi-user.target
```
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now dstock
sudo systemctl status dstock      # Active(running) を確認
curl http://127.0.0.1:5080/api/meta   # 全銘柄数が返れば成功
```
> **本番(Production)では自動取得が既定で有効**(`appsettings.Production.json` の `Fetch:Enabled=true`)です。
> robots.txt 順守・既定8秒間隔で全銘柄を巡回取得し、6日以内に取得済みの銘柄はスキップ(週次更新相当)。
> サーバ負荷・規約に配慮した低頻度運用です。停止したい場合は上記 `Fetch__Enabled=false` を設定して再起動。

### 手順 4: nginx でリバースプロキシ(80番ポート公開)
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

### 手順 5: ファイアウォール / HTTPS(任意)
```bash
sudo ufw allow OpenSSH
sudo ufw allow 'Nginx HTTP'
sudo ufw enable
```
- ConoHa の **セキュリティグループ**でも 80(/443)番の許可が必要です。
- 独自ドメインがある場合は `certbot`(Let's Encrypt)で HTTPS 化を推奨:
  ```bash
  sudo apt-get install -y certbot python3-certbot-nginx
  sudo certbot --nginx -d your-domain.example
  ```
- 公開範囲を限定したい場合は nginx の `allow/deny` や Basic 認証、ConoHa セキュリティグループで送信元 IP を制限してください
  (本アプリに認証機能はありません)。

### 手順 6: 更新(再デプロイ)
```bash
cd /var/www/DStockAnalysis && git pull
dotnet publish DStockAnalysis.Web -c Release -o /var/www/dstock
sudo systemctl restart dstock
```

### 自動取得の設定(`appsettings.Production.json` の `Fetch`)
| キー | 既定 | 説明 |
|---|---|---|
| `Enabled` | `true` | 自動取得の有効/無効 |
| `Scope` | `"all"` | `all`=全銘柄 / `watchlist`=`DataDir/codes.txt` の銘柄のみ |
| `DelaySeconds` | `8` | リクエスト間隔の基準秒(robots の Crawl-delay と大きい方を採用) |
| `MaxAgeDays` | `6` | この日数以内に取得済みの銘柄は再取得しない(週次運用) |
| `CycleRestHours` | `24` | 全銘柄1巡後、次巡まで待つ時間 |
| `UseKabutan` | `false` | 株探も対象に含める(Crawl-delay 3秒順守) |

> 全銘柄(約3,700)を 8 秒間隔で巡回するため 1 巡には時間がかかります。短期売買用ではないため
> この低速・週次運用で十分です。取得状況は `GET /api/admin/fetch/status` で確認できます。

---

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

### 1-2. 実際の指標値(実データ)の取得について
個別銘柄分析画面のヘッダーに、銘柄ごとの **外部データソースへのリンク** を用意しました(クリックで既定ブラウザが開きます)。
ここから最新の実数値を確認し、必要に応じて CSV 取込でアプリへ反映できます。

| リンク | 用途 | URL 形式 |
|---|---|---|
| IR BANK | 通期業績・配当・CF・財務 | `https://irbank.net/{コード}` |
| バフェット・コード | 各種指標・スコア | `https://www.buffett-code.com/company/{コード}/` |
| 株探 | 株価・PER/PBR・業績速報 | `https://kabutan.jp/stock/?code={コード}` |
| 株マップ | 指標・チャート | `https://jp.kabumap.com/...&codetext={コード}` |
| みんかぶ | 株価・予想・指標 | `https://minkabu.jp/stock/{コード}` |
| 株主優待ガイド | 優待内容・優待利回り | `https://minkabu.jp/stock/{コード}/yutai` |

> **実データ取得の方針**: 各サイトの **robots.txt を順守**し、許可ページのみを **間隔を空けて(既定8秒)**、
> **週1回・ウォッチリスト限定**で取得します(短期売買用ではないため低頻度で十分)。robots.txt が自動アクセスを
> 拒否する **バフェット・コードは対象外**(リンクのみ)。詳細は `docs/データ取得手順.md`。

### 実データ取得フロー(要点)
1. **全銘柄リスト(実データ)は自動**: `tools\update_master.bat` か「全銘柄更新(JPX)」で最新化。
2. **主要指標を自動取得**: `tools\fetch_data.bat` を実行(要 Python・pip不要)。IR BANK/みんかぶ/株探/株マップから
   PER・PBR・ROE・配当利回り・時価総額・EPS 等を取得し `tools\output\stocks_real.csv` を生成。
   `codes.txt` で対象銘柄を指定。タスクスケジューラで週1自動実行も可。
3. **CSV取込**: 生成された CSV をアプリで取込。**取得できた列だけ**を実データで上書き(列単位マージ)。
4. **その他指標は補完**: 財務詳細/CF/株主優待などは「テンプレ出力」＋リンク(`tools\open_sources.bat 7203`)で記入して取込。
5. **保存**: 次回起動時も再現。

> 取得値は各サイトの基準・タイミングでばらつくため **ベストエフォート**です(取込前に CSV で確認・修正可)。
> 過去履歴まで含む完全自動は J-Quants/EDINET が必要ですが、過去情報が有料のため本構成では使いません。

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
dotnet test DStockAnalysis.sln
```
- `Tests/`(WPF/Core): 単体(Service/Model/Common)＋結合(ViewModel 画面横断フロー)= **77 件**
- `Tests.Web/`(Web): 単体(スクレイパ純粋関数)＋結合(StockStore・HTTP API)= **25 件**
- 合計 **102 件・0 失敗**(2026/06/22 実行)。

個別実行も可能です:
```
dotnet test Tests/DStockAnalysis.Tests.csproj        # WPF/Core
dotnet test Tests.Web/DStockAnalysis.Web.Tests.csproj # Web
```
詳細は `docs/単体試験項目書.md` / `docs/結合試験項目書.md` を参照してください。

## ドキュメント

| 種別 | ファイル |
|---|---|
| データ取得手順 | `docs/データ取得手順.md` |
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
DStockAnalysis.sln                 … 全プロジェクトのソリューション

DStockAnalysis.Core/               … 共有ドメイン(net8.0、WPF/Web 双方が参照)
  Common/                          … ObservableObject, Grades
  Models/                          … Stock, TimeSeriesPoint, BuffettCheck, StockMemo,
                                      ScreeningCriteria, ScreeningPreset, AppData, Enums
  Services/                        … CsvImportService, DataStorageService, ScoreService,
                                      SampleDataService, PresetService, JpxMasterService,
                                      IndicatorSeedService, ReferenceLinkService

DStockAnalysis.csproj(WPF)        … デスクトップ版(Core を参照)
  App.xaml(.cs) / MainWindow / Themes/DarkTheme.xaml / Converters/ / ViewModels/ / Views/
  app.manifest / Data/data_j.xls

DStockAnalysis.Web/                … Web 版(ASP.NET Core、Core を参照)
  Program.cs                       … 最小API・DI・静的配信
  Services/StockStore.cs           … サーバ側の銘柄ストア(WPF の MainViewModel 相当)
  Services/IndicatorFetchService.cs… 指標スクレイパ(fetch_data.py の C# 移植・robots順守)
  Services/IndicatorFetchHostedService.cs … 全銘柄を巡回取得する常駐サービス
  Models/Dtos.cs                   … API 用 DTO
  wwwroot/                         … index.html, css/styles.css, js/app.js(3画面SPA)
  appsettings*.json

Tests/                             … WPF/Core の xUnit(単体・結合)
Tests.Web/                         … Web の xUnit(単体・結合, WebApplicationFactory)
sample_stocks.csv / run.bat / tools/ / docs/
```

## 将来拡張の指針

- グラフは `ViewModels/ChartModels.cs` の `ChartGroup/ChartSeries/ChartBar` に依存しているため、
  描画部(`App.xaml` の `ChartGroup` テンプレート)を **LiveCharts2** などに差し替え可能。
- データ取得は `Services` に新規サービス(`JQuantsService` 等)を追加し、
  `CsvImportService` と同様に `List<Stock>` を返す形にすれば `MainViewModel` から差し込めます。
