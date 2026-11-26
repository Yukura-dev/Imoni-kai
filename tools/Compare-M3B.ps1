param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m3b'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($screen in @('30_Game_start', '31_Game_collecting', '33_GameMid_bonus')) {
    $mock = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "design/screenshots/$screen.png"))
    $unity = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "Library/M3B-$screen.png"))
    $canvas = [System.Drawing.Bitmap]::new(780, 844)
    $drawing = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) { throw "Unexpected Unity capture size for $screen" }
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.DrawImage($mock, [System.Drawing.Rectangle]::new(0, 0, 390, 844))
        $drawing.DrawImage($unity, [System.Drawing.Rectangle]::new(390, 0, 390, 844))
        $canvas.Save((Join-Path $destination "compare_$screen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $drawing.Dispose(); $canvas.Dispose(); $mock.Dispose(); $unity.Dispose()
    }
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M3B-$screen.png") -Destination (Join-Path $destination "unity_$screen.png")
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M3B-$screen.json") -Destination (Join-Path $destination "state_$screen.json")
}
foreach ($index in 0..7) {
    $name = 'sequence_{0:00}' -f $index
    foreach ($extension in @('png', 'json')) {
        Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M3B-$name.$extension") -Destination (Join-Path $destination "$name.$extension")
    }
}
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'Library/M3B-sequence.json') -Destination (Join-Path $destination 'sequence.json')
Write-Output "Saved three side-by-side comparisons and eight sequence frames to $destination"
