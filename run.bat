@echo off
chcp 65001 >nul
rem ===== DStockAnalysis 起動用バッチ =====
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET 8 SDK / ランタイムが見つかりません。
    echo https://dotnet.microsoft.com/download/dotnet/8.0 からインストールしてください。
    pause
    exit /b 1
)

echo DStockAnalysis をビルドして起動します...
dotnet run --project "%~dp0DStockAnalysis.csproj" -c Release
if errorlevel 1 (
    echo 起動に失敗しました。エラー内容を確認してください。
    pause
)
