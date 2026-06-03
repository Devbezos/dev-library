param(
    [string]$JsonFile = "tcg_urls_to_insert.json",
    [string]$OutSql = "seed_tcg_store_urls.sql",
    [string]$TablePrefix = "tcg_"
)

$inPath = Join-Path -Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) -ChildPath $JsonFile
$outPath = Join-Path -Path (Split-Path -Parent $MyInvocation.MyCommand.Definition) -ChildPath $OutSql

if (-not (Test-Path $inPath)) {
    Write-Error "JSON file not found: $inPath"
    exit 1
}

$items = Get-Content $inPath -Raw | ConvertFrom-Json

"-- Generated SQL to insert stores and urls into ${TablePrefix}stores and ${TablePrefix}store_urls" | Out-File -FilePath $outPath -Encoding utf8
"START TRANSACTION;" | Out-File -FilePath $outPath -Append -Encoding utf8

foreach ($it in $items) {
    $name = ($it.store -replace "'","''")
    $display = ($it.displayName -replace "'","''")
    $game = ($it.game -replace "'","''")
    $category = ($it.category -replace "'","''")
    $url = ($it.url -replace "'","''")
    $enabled = if ($null -eq $it.enabled) { 1 } else { [int]$it.enabled }

    $insertStore = "INSERT IGNORE INTO ${TablePrefix}stores (name, display_name) VALUES ('$name', '$display');" 
    $insertUrl = "INSERT IGNORE INTO ${TablePrefix}store_urls (store_id, game, category, url, enabled) VALUES ((SELECT id FROM ${TablePrefix}stores WHERE name = '$name' LIMIT 1), '$game', '$category', '$url', $enabled);"

    $insertStore | Out-File -FilePath $outPath -Append -Encoding utf8
    $insertUrl | Out-File -FilePath $outPath -Append -Encoding utf8
}

"COMMIT;" | Out-File -FilePath $outPath -Append -Encoding utf8

Write-Host "Wrote SQL to: $outPath" -ForegroundColor Green
Write-Host "Run with your MySQL client, e.g.: mysql -u user -p -h host < $outPath" -ForegroundColor Yellow
