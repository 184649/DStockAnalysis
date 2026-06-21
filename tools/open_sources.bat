@echo off
chcp 65001 >nul
setlocal
rem 指定銘柄の外部データソース(IR BANK/株探/バフェット・コード/みんかぶ/みんかぶ優待/株マップ)を
rem 既定ブラウザで一括オープンします。最新の実数値を確認して CSV に転記する作業を効率化します。
rem 使い方: open_sources.bat 7203   (引数なしの場合は入力を求めます)

set "CODE=%~1"
if "%CODE%"=="" set /p CODE=銘柄コードを入力 (例 7203):
if "%CODE%"=="" goto :eof

echo %CODE% の各サイトを開きます...
start "" "https://irbank.net/%CODE%"
start "" "https://kabutan.jp/stock/?code=%CODE%"
start "" "https://www.buffett-code.com/company/%CODE%/"
start "" "https://minkabu.jp/stock/%CODE%"
start "" "https://minkabu.jp/stock/%CODE%/yutai"
start "" "https://jp.kabumap.com/servlets/kabumap/Action?SRC=basic/base/main&codetext=%CODE%"
