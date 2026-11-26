param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m4'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($screen in @('32_Game_shop', '34_GameMid_menu', '35_GameWarehouse', '36_GameUpgrade')) {
    $mock = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "design/screenshots/$screen.png"))
    $unity = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "Library/M4-$screen.png"))
    $canvas = [System.Drawing.Bitmap]::new(780, 844)
    $drawing = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) { throw "Unexpected Unity capture size for $screen" }
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.DrawImage($mock, [System.Drawing.Rectangle]::new(0, 0, 390, 844))
        $drawing.DrawImage($unity, [System.Drawing.Rectangle]::new(390, 0, 390, 844))
        $canvas.Save((Join-Path $destination "compare_$screen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $drawing.Dispose(); $canvas.Dispose(); $mock.Dispose(); $unity.Dispose() }
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M4-$screen.png") -Destination (Join-Path $destination "unity_$screen.png")
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M4-$screen.json") -Destination (Join-Path $destination "state_$screen.json")
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library') -File -Filter 'M4-proof_*') {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name.Substring(3))
}
Write-Output "Saved four comparisons and interaction evidence to $destination"
