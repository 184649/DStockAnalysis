@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title DStockAnalysis データ取得

rem === 対象6サイト(robots.txt順守・間隔を空けて取得)から指標を取得し、
rem === output\stocks_real.csv を生成します。週1回程度の実行を想定しています。

where python >nul 2>&1
if errorlevel 1 (
    echo [エラー] Python が見つかりません。
    echo https://www.python.org/downloads/ からインストールしてください(pip不要・標準ライブラリのみ)。
    echo インストール時に "Add python.exe to PATH" にチェックしてください。
    echo.
    pause
    exit /b 1
)

echo ============================================
echo  データ取得を開始します(robots.txt順守・低速)
echo  対象銘柄は codes.txt で編集できます
echo  引数はそのまま fetch_data.py へ渡します
echo   例) fetch_data.bat --force
echo       fetch_data.bat --test 7203
echo       fetch_data.bat --kabutan
echo ============================================
echo.

python "%~dp0fetch_data.py" %*
set RC=%ERRORLEVEL%

echo.
if "%RC%"=="0" (
    echo 完了しました。アプリの「CSV取込」で次のファイルを読み込んでください:
    echo   %~dp0output\stocks_real.csv
) else (
    echo 取得に失敗した可能性があります。メッセージを確認してください。
)
echo.
pause
