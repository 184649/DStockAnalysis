@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title DStockAnalysis Web (実データ版)

rem ===== .NET 8 の有無を確認 =====
where dotnet >nul 2>&1
if errorlevel 1 goto :nodotnet

echo ============================================
echo  DStockAnalysis Web (実データ版) を起動します
echo  ・銘柄を開くと Yahoo/株探等から実データを取得します
echo  ・起動後、ブラウザで http://localhost:5000 を開きます
echo  ・初回ビルドは数十秒かかる場合があります
echo  ・終了するには、このウィンドウで Ctrl+C を押してください
echo ============================================
echo.

rem オンデマンド取得＋株価最新化のみ(全銘柄巡回スクレイプはしない)
set ASPNETCORE_ENVIRONMENT=Development

rem サーバ起動の少し後にブラウザを開く
start "" /b cmd /c "timeout /t 12 /nobreak >nul & start http://localhost:5000"

dotnet run --project "DStockAnalysis.Web\DStockAnalysis.Web.csproj" -c Release --urls http://localhost:5000
goto :eof

:nodotnet
echo.
echo [エラー] .NET 8 ランタイム / SDK が見つかりません。
echo 次のページからインストールしてください:
echo   https://dotnet.microsoft.com/download/dotnet/8.0
echo インストール後、PC を再起動してから再度 run-web.bat を実行してください。
echo.
pause
exit /b 1
