param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m6'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$pairs = @(
    @{ Name='43_Result'; Reference='design/screenshots/43_Result.png' },
    @{ Name='Clear'; Reference='docs/validation/m5/proof_normal_clear.png' },
    @{ Name='44_ImoniFail_mid'; Reference='design/screenshots/44_ImoniFail_mid.png' },
    @{ Name='45_ImoniFail_end'; Reference='design/screenshots/45_ImoniFail_end.png' }
)
foreach ($pair in $pairs) {
    $mock = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot $pair.Reference))
    $unity = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "Library/M6-$($pair.Name).png"))
    $canvas = [System.Drawing.Bitmap]::new(780,844)
    $drawing = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) { throw 'Set Game view to 390x844.' }
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.DrawImage($mock,[System.Drawing.Rectangle]::new(0,0,390,844))
        $drawing.DrawImage($unity,[System.Drawing.Rectangle]::new(390,0,390,844))
        $canvas.Save((Join-Path $destination "compare_$($pair.Name).png"),[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $drawing.Dispose(); $canvas.Dispose(); $mock.Dispose(); $unity.Dispose() }
    foreach ($extension in @('png','json')) {
        Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M6-$($pair.Name).$extension") -Destination (Join-Path $destination "unity_$($pair.Name).$extension")
    }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library') -File -Filter 'M6-proof_*') {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name.Substring(3))
}
'Saved four comparisons: left reference, right Unity.'
