param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $ProjectRoot
$destination = Join-Path $ProjectRoot 'docs/validation/m8'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
function Review7($request) { & "$PSScriptRoot/Invoke-M7Review.ps1" -RequestJson ($request | ConvertTo-Json -Compress) -TimeoutSeconds 120 | ConvertFrom-Json }
function Review6($request) { & "$PSScriptRoot/Invoke-M6Review.ps1" -RequestJson ($request | ConvertTo-Json -Compress) | ConvertFrom-Json }
function Review8($request) { & "$PSScriptRoot/Invoke-M8Review.ps1" -RequestJson ($request | ConvertTo-Json -Compress) -TimeoutSeconds 120 | ConvertFrom-Json }
function Save-Json($value, $path) { [IO.File]::WriteAllText($path, ($value | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false)) }
function Capture($name, $folder) {
    $result = Review7 @{command='capture'; name=$name}
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    Copy-Item -LiteralPath $result.path -Destination (Join-Path $folder "$name.png") -Force
    Copy-Item -LiteralPath ([IO.Path]::ChangeExtension($result.path, '.json')) -Destination (Join-Path $folder "$name.json") -Force
    Save-Json (Review8 @{command='audit'}) (Join-Path $folder "$name-render.json")
}
$null = Review7 @{command='game-view'}
$null = Review6 @{command='preview'; failed=$false}
foreach ($sample in @(@('40_Imoni_start',1500), @('41_Imoni_mid',4600), @('42_Imoni_finale',9000))) {
    $null = Review6 @{command='sample'; timeMs=$sample[1]}
    Capture $sample[0] $destination
}
$null = Review6 @{command='preview'; failed=$true}
foreach ($sample in @(@('44_ImoniFail_mid',4000), @('45_ImoniFail_end',9000))) {
    $null = Review6 @{command='sample'; timeMs=$sample[1]}
    Capture $sample[0] $destination
}
foreach ($screen in @(@('Home','10_Home'),@('Waiting','15_Waiting'),@('Loading','17_Loading'))) {
    $null = Review6 @{command='show'; screen=$screen[0]}
    if ($screen[0] -eq 'Waiting') { Start-Sleep -Milliseconds 800 }
    Capture $screen[1] $destination
}
$null = Review6 @{command='prepare'; seed=482}
Save-Json (Review8 @{command='stable'; samples=100}) (Join-Path $destination 'render-native-stable.json')
Save-Json (Review8 @{command='tap-ground'; x=34; y=33}) (Join-Path $destination 'diagonal-tap.json')
for ($index=0; $index -le 6; $index++) {
    if ($index -gt 0) { $null = Review6 @{command='step'; seconds=0.1} }
    Capture ('diagonal_{0:00}' -f $index) (Join-Path $destination 'movement')
}
foreach ($device in @(@('Samsung Galaxy Note10','Note10'),@('Apple iPhone 12','iPhone12'))) {
    $null = Review7 @{command='simulator'; device=$device[0]}
    $null = Review6 @{command='prepare'; seed=482}
    $folder = Join-Path $destination "simulator/$($device[1])"
    Capture 'game' $folder
    Save-Json (Review8 @{command='stable'; samples=100}) (Join-Path $folder 'stable.json')
    foreach ($failed in @($false,$true)) {
        $null = Review6 @{command='preview'; failed=$failed}
        $null = Review6 @{command='sample'; timeMs=9000}
        Capture $(if ($failed) {'effect-fail'} else {'effect-success'}) $folder
    }
}
$null = Review7 @{command='game-view'}
Save-Json (Review6 @{command='console'}) (Join-Path $destination 'unity-console.json')
Write-Output 'M8 captures saved. Inspect the actual PNGs before accepting them.'
