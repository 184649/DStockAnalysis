# Download JPX "Listed Issues" master (data_j.xls) into the app cache folder.
# After running, start the app (or press "全銘柄更新(JPX)") to load the latest full universe.
# (ASCII-only on purpose, so Windows PowerShell reads it correctly regardless of locale.)
$ErrorActionPreference = 'Stop'
$url = 'https://www.jpx.co.jp/markets/statistics-equities/misc/tvdivq0000001vg2-att/data_j.xls'
$dir = Join-Path $env:APPDATA 'DStockAnalysis'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$dst = Join-Path $dir 'data_j.xls'

Write-Host '============================================'
Write-Host ' JPX listed-issues master downloader'
Write-Host '============================================'
Write-Host "Source : $url"
Write-Host "Target : $dst"
try {
    Invoke-WebRequest -Uri $url -OutFile $dst -UseBasicParsing -TimeoutSec 60
    $size = (Get-Item $dst).Length
    if ($size -lt 100000) { throw "Downloaded file is too small ($size bytes). Download may have failed." }
    Write-Host ("OK: saved {0:N0} bytes." -f $size) -ForegroundColor Green
    Write-Host 'Done. Start DStockAnalysis to load the latest stocks (or press the update button if running).'
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Check your internet connection and run again.'
    exit 1
}
