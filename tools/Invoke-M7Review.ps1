param([Parameter(Mandatory = $true)][string]$RequestJson,
      [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent), [int]$TimeoutSeconds = 25)
$ErrorActionPreference = 'Stop'
$requestPath = Join-Path $ProjectRoot 'Library/M7Review.request'
$resultPath = Join-Path $ProjectRoot 'Library/M7Review.result'
$null = $RequestJson | ConvertFrom-Json
if (Test-Path -LiteralPath $requestPath) { throw 'A previous Editor request is still pending.' }
$previousWrite = if (Test-Path -LiteralPath $resultPath) { (Get-Item -LiteralPath $resultPath).LastWriteTimeUtc } else { [datetime]::MinValue }
[IO.File]::WriteAllText($requestPath, $RequestJson, [Text.UTF8Encoding]::new($false))
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
do {
    Start-Sleep -Milliseconds 100
    if (-not (Test-Path -LiteralPath $requestPath) -and (Test-Path -LiteralPath $resultPath) -and
        (Get-Item -LiteralPath $resultPath).LastWriteTimeUtc -gt $previousWrite) {
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($result.status -eq 'failed') { throw $result.failure }
        $result | ConvertTo-Json -Depth 12
        return
    }
} while ([DateTime]::UtcNow -lt $deadline)
throw 'Unity did not complete the request. Check compilation and Library/M7Review.result.'
