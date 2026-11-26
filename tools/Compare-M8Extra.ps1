param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m8/extra'
$after = Join-Path $destination 'after'
[IO.Directory]::CreateDirectory($after) | Out-Null
$pairs = [ordered]@{
    Main_loading = @('M2B-extra-before-main-loading','M2B-extra-after-main-loading')
    Main_ready = @('M2B-extra-before-main-ready','M2B-extra-after-main-ready')
    Main_settings = @('M2B-extra-before-main-ready','M2B-extra-after-main-settings')
    Home = @('M2B-extra-before-home','M2B-extra-after-home')
    Settings = @('M2B-extra-before-settings','M2B-extra-after-settings')
    Account_switch = @('M2B-extra-before-login','M2B-extra-after-accounts')
    Tutorial2 = @('M2B-extra-before-tutorial2','M2B-extra-after-tutorial2')
    Tutorial3 = @('M2B-extra-before-tutorial3','M2B-extra-after-tutorial3')
    Loading = @('M2B-extra-before-loading','M2B-extra-after-loading')
    Game_labels = @('M7-extra-before-game-labels','M7-extra-after-game-labels')
    GameOver = @('M7-extra-before-gameover','M7-extra-after-gameover')
}
foreach ($name in $pairs.Keys) {
    $names = $pairs[$name]
    $beforePath = Join-Path $destination ('before/' + $names[0] + '.png')
    $afterPath = Join-Path $after ($names[1] + '.png')
    Copy-Item -LiteralPath (Join-Path $ProjectRoot ('Library/' + $names[1] + '.png')) -Destination $afterPath -Force
    $left = [Drawing.Image]::FromFile($beforePath)
    $right = [Drawing.Image]::FromFile($afterPath)
    try {
        if ($left.Width -ne 390 -or $left.Height -ne 844 -or $right.Width -ne 390 -or $right.Height -ne 844) { throw 'Expected two unscaled 390x844 captures.' }
        $bitmap = [Drawing.Bitmap]::new(780,844)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.DrawImageUnscaled($left,0,0)
            $graphics.DrawImageUnscaled($right,390,0)
            $bitmap.Save((Join-Path $destination ('compare_' + $name + '.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    } finally { $left.Dispose(); $right.Dispose() }
    Write-Output $name
}
