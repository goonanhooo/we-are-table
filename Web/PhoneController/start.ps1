$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$unityCandidate = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $env:UNITY_PROJECT -and (Test-Path -LiteralPath (Join-Path $unityCandidate 'Assets'))) {
    $env:UNITY_PROJECT = $unityCandidate
}
if (-not (Test-Path -LiteralPath 'node_modules')) {
    Write-Host 'Install dependencies first: npm install'
    exit 1
}
$taskRelay = $null
$taskExisting = $false
try { $taskExisting = (Invoke-RestMethod -Uri 'http://127.0.0.1:4180/health' -TimeoutSec 1).ok } catch {}
if (-not $taskExisting) {
    $taskRelay = Start-Process -FilePath 'node' -ArgumentList 'server.mjs' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru
}
try {
    Start-Sleep -Seconds 2
    Start-Process 'http://127.0.0.1:4180/host'
    Write-Host 'Keep this window open while playing. Ctrl+C stops the Internet tunnel.'
    & node internet.mjs
} finally {
    if ($null -ne $taskRelay -and -not $taskRelay.HasExited) { Stop-Process -Id $taskRelay.Id }
}
