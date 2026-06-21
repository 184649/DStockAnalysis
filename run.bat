@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title DStockAnalysis

set "EXE=%~dp0bin\Release\net8.0-windows\DStockAnalysis.exe"

rem ===== .NET 8 の有無を確認 =====
where dotnet >nul 2>&1
if errorlevel 1 goto :nodotnet

echo ============================================
echo  DStockAnalysis をビルドして起動します
echo  (初回ビルドは数十秒かかる場合があります)
echo ============================================
echo.

dotnet build "DStockAnalysis.csproj" -c Release -v minimal
if errorlevel 1 goto :buildfail

:run
if not exist "%EXE%" goto :noexe
echo.
echo アプリを起動します...
start "" "%EXE%"
exit /b 0

:buildfail
echo.
echo [警告] ビルドに失敗しました。
if exist "%EXE%" (
    echo 既存のビルド済みアプリを起動します...
    start "" "%EXE%"
    exit /b 0
)
echo.
echo ヒント: このフォルダが "C:\Program Files" など書き込み不可の場所にある場合、
echo         ビルドに失敗することがあります。ドキュメント等の書き込み可能な
echo         フォルダにコピーしてから run.bat を実行してください。
echo.
pause
exit /b 1

:noexe
echo.
echo [エラー] 実行ファイルが見つかりません:
echo   %EXE%
echo ビルドが完了しているか確認してください。
echo.
pause
exit /b 1

:nodotnet
echo.
echo [エラー] .NET 8 ランタイム / SDK が見つかりません。
echo 次のページからインストールしてください:
echo   https://dotnet.microsoft.com/download/dotnet/8.0
echo インストール後、PC を再起動してから再度 run.bat を実行してください。
echo.
pause
exit /b 1
