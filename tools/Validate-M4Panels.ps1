param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$checks = [Collections.Generic.List[string]]::new()
function Send([hashtable]$command) {
    & (Join-Path $PSScriptRoot 'Invoke-M4Review.ps1') -RequestJson ($command | ConvertTo-Json -Compress) -ProjectRoot $ProjectRoot | Out-Null
}
function State {
    Send @{ command = 'inspect' }
    Get-Content -LiteralPath (Join-Path $ProjectRoot 'Library/M4-inspect.json') -Raw | ConvertFrom-Json
}
function Check([bool]$condition, [string]$description) {
    if (-not $condition) { throw $description }
    $checks.Add($description)
}
function Click([string]$name) { Send @{ command = 'click'; name = $name } }
function Row($state, [string]$material) { $state.inventory | Where-Object { $_.material -eq $material -and $_.quality -eq 'normal' } }
function Enabled($state, [string]$name) { ($state.buttons | Where-Object name -eq $name).enabled }
$report = [ordered]@{ passed = $false; checks = @(); method = 'Attached panel callbacks and real IGameServer commands, with isolated review sessions' }
try {
    Send @{ command = 'prepare'; seed = 202647 }
    Send @{ command = 'open'; panel = 'shop' }
    Click 'Buy_wood_50'; Click 'Buy_taro_10'
    Send @{ command = 'open'; panel = 'warehouse' }
    Send @{ command = 'select'; material = 'wood'; quality = 'normal' }
    Click 'ProtectIncrease'; $state = State
    Check ((Row $state 'wood').protected -eq '5') 'Increase protection by configured five'
    Click 'ProtectDecrease'; $state = State
    Check ((Row $state 'wood').protected -eq '0') 'Decrease protection by configured five'
    Click 'ToggleProtection'; $state = State
    Check ((Row $state 'wood').protected -eq '50') 'Protect the whole selected row'
    Click 'ToggleProtection'; $state = State
    Check ((Row $state 'wood').protected -eq '0') 'Release whole-row protection'
    foreach ($pair in @(@(25,12), @(50,25), @(75,37), @(100,50))) {
        Click ('SalePercent' + $pair[0]); $state = State
        Check ($state.saleQuantity -eq [string]$pair[1]) ('Configured percentage ' + $pair[0] + ' uses sellable quantity')
    }
    Send @{ command = 'input'; name = 'SaleQuantity'; value = '7' }; Click 'SaleSelected'; $state = State
    Check ((Row $state 'wood').quantity -eq '43' -and $state.funds -eq '414') 'Direct quantity sells immediately through the server'
    Click 'ProtectIncrease'
    Send @{ command = 'select'; material = 'taro'; quality = 'normal' }; Click 'ToggleProtection'
    Click 'SellAllUnprotected'; $state = State
    Check ((Row $state 'wood').quantity -eq '5' -and (Row $state 'taro').quantity -eq '10' -and $state.funds -eq '490') 'Bulk sale preserves protection across material rows'
    Check (-not (Enabled $state 'SellAllUnprotected')) 'Bulk sale disables when only protected stock remains'
    $historyLines = @($state.layout | Where-Object { $_.classes -contains 'operation-history__line' -and $_.display -ne 'None' })
    Check ($historyLines.Count -eq 3 -and @($historyLines | Where-Object { $_.text -match '\[\d+:\d\d\]' }).Count -eq 3) 'Three timestamped operation-history rows are visible'
    Send @{ command = 'open'; panel = 'shop' }; Click 'Buy_negi_10'
    Send @{ command = 'open'; panel = 'upgrade' }; Click 'Upgrade_move'; $state = State
    Check ($state.players[0].upgrades.move -eq 1 -and $state.players[1].upgrades.move -eq 0 -and $state.funds -eq '160') 'Upgrade changes only the acting player and shared funds'
    Click 'Upgrade_speed'; $state = State
    Check ($state.players[0].upgrades.speed -eq 1 -and $state.funds -eq '10') 'Second upgrade uses its own configured level and price'
    Check (@($state.buttons | Where-Object { $_.name -match '^Upgrade_' -and $_.enabled }).Count -eq 0) 'All upgrades disable below their prices'
    Send @{ command = 'step'; seconds = 3 }; $state = State
    Check ($state.elapsed -eq 3 -and -not $state.toastVisible -and -not $state.worldInputEnabled) 'Panel keeps simulation ticking while hiding toast and blocking map input'
    Send @{ command = 'close' }; $state = State
    Check ($state.panel -eq 'none' -and $state.worldInputEnabled -and -not $state.toastVisible) 'Closing restores map input without replaying stale toast'
    Send @{ command = 'open'; panel = 'menu' }; Click 'ReturnHome'; $state = State
    Check ($state.screen -eq 'Home') 'Menu return-home callback navigates to Home'
    $report.passed = $true
} catch { $report.failure = $_.ToString(); throw }
finally {
    $report.checks = @($checks.ToArray())
    $report.utc = [DateTime]::UtcNow.ToString('O')
    $destination = Join-Path $ProjectRoot 'docs/validation/m4/panel-integration.json'
    $report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $destination -Encoding utf8
}
Write-Output ('Passed ' + $checks.Count + ' attached panel integration checks.')
