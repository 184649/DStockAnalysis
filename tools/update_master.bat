@echo off
chcp 65001 >nul
rem JPX 上場銘柄一覧(全銘柄)を最新化するバッチ。ダブルクリックで実行できます。
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update_master.ps1"
echo.
pause
