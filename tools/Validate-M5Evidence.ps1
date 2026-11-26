param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$evidence = Join-Path $ProjectRoot 'docs/validation/m5'
$checks = [Collections.Generic.List[object]]::new()
function Read-State([string]$name) { Get-Content -LiteralPath (Join-Path $evidence "$name.json") -Raw | ConvertFrom-Json }
function Check([string]$name, [bool]$passed) {
    $checks.Add([pscustomobject]@{ name = $name; passed = $passed })
    if (-not $passed) { throw "M5 evidence failed: $name" }
}
$settled = Read-State 'proof_timeup'
$next = Read-State 'proof_next_round'
Check 'Time up stops at 300 seconds' ($settled.elapsed -eq 300 -and $settled.phase -eq 'Succeeded')
Check 'Funds and upgrade survive next round' ($settled.funds -eq $next.funds -and $next.players[0].upgrades.speed -eq 1)
Check 'Same world and resource growth survive next round' ($settled.worldIdentityHash -eq $next.worldIdentityHash -and $settled.resourceStateHash -eq $next.resourceStateHash)
Check 'Stock, protection, and streak reset' ($next.inventoryCount -eq 0 -and $next.protectionCount -eq 0 -and @($next.players | Where-Object streak -ne 0).Count -eq 0)
Check 'Next round expands exploration' ($next.round -eq 2 -and $next.unlockedRadius -gt $settled.unlockedRadius)
$retry = Read-State 'proof_retry_round1'
Check 'Retry uses new seed and initial state' ($retry.seed -ne $settled.seed -and $retry.round -eq 1 -and $retry.funds -eq '1000' -and $retry.inventoryCount -eq 0 -and $retry.players[0].upgrades.speed -eq 0)
Check 'Retry passes through Loading' ((Read-State 'proof_retry_loading').screen -eq 'Loading')
Check 'Actual success effect uses Core score' ((Read-State 'proof_actual_success_effect').effect.finalScore -eq $settled.result.score)
$failure = Read-State 'proof_actual_fail_effect'
Check 'Actual failed effect uses frozen Core score and missing materials' ($failure.effect.failed -and $failure.effect.finalScore -eq $failure.result.score -and $failure.result.missing.Count -gt 0)
$replay = Read-State 'proof_replay'
$skip = Read-State 'proof_skip_result'
Check 'Replay and skip do not settle again' ($replay.funds -eq $skip.funds -and $replay.cumulative -eq $skip.cumulative -and $skip.screen -eq 'Result')
$short = Read-State 'proof_short_round_timeup'
Check 'Short rounds run at 10x and stop at deadline' ($short.shortRounds -and $short.clockScale -eq 10 -and $short.elapsed -eq 300)
$clear = Read-State 'proof_normal_clear'
Check 'Five successes reach Clear without R6' ($clear.screen -eq 'Clear' -and $clear.round -eq 5 -and $clear.phase -eq 'Cleared')
Check 'Clear and game over return Home' ((Read-State 'proof_clear_home').screen -eq 'Home' -and (Read-State 'proof_gameover_home').screen -eq 'Home')
foreach ($item in @(@('40_Imoni_start',1500),@('41_Imoni_mid',4600),@('42_Imoni_finale',9000),@('44_ImoniFail_mid',4000),@('45_ImoniFail_end',9000))) {
    $state = Read-State ('state_' + $item[0])
    Check ('Exact capture time: ' + $item[0]) ($state.effect.timeMs -eq $item[1] -and $state.width -eq 390 -and $state.height -eq 844)
}
foreach ($name in @('imoni_success','imoni_fail')) {
    $video = Read-State ($name + '-telemetry')
    Check ("315 clean frames: $name") ($video.frames.Count -eq 315 -and @($video.frames | Where-Object { $_.console.errors -ne 0 -or $_.console.warnings -ne 0 }).Count -eq 0)
    Check ("Result visible and exact score before 10 seconds: $name") (@($video.frames | Where-Object { $_.effect.timeMs -le 10000 -and $_.effect.resultVisible -and $_.effect.displayScore -eq $_.effect.finalScore }).Count -gt 0)
}
$states = Get-ChildItem -LiteralPath $evidence -File | Where-Object { $_.Name -like 'state_*.json' -or $_.Name -like 'proof_*.json' }
Check 'All stills have zero errors and warnings' (@($states | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } | Where-Object { $_.console.errors -ne 0 -or $_.console.warnings -ne 0 }).Count -eq 0)
$report = [pscustomobject]@{ checkedAtUtc = [datetime]::UtcNow.ToString('O'); passed = $checks.Count; failed = 0; checks = $checks }
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $evidence 'round-flow-validation.json') -Encoding utf8
"M5 evidence: $($checks.Count) passed, 0 failed."
