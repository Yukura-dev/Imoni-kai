[CmdletBinding()]
param(
    [string[]]$Names = @(
        '10_Home', '15_Waiting', '17_Loading', '40_Imoni_start', '41_Imoni_mid',
        '42_Imoni_finale', '44_ImoniFail_mid', '45_ImoniFail_end'
    ),
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent)
)

# Example: ./tools/Compare-M8.ps1 -Names '40_Imoni_start','45_ImoniFail_end'
# Produces evidence only; visual agreement and Console status require a separate review.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$destination = Join-Path $ProjectRoot 'docs/validation/m8'
if (-not $Names -or $Names.Count -eq 0) { throw 'Select at least one capture name.' }

$pairs = foreach ($name in ($Names | Select-Object -Unique)) {
    if ($name -notmatch '^[A-Za-z0-9_-]+$') { throw "Invalid capture name: $name" }
    $reference = if ($name -eq 'Clear') { 'docs/validation/m5/proof_normal_clear.png' } else { "design/screenshots/$name.png" }
    $pair = [PSCustomObject]@{
        Name = $name
        Reference = Join-Path $ProjectRoot $reference
        Capture = Join-Path $destination "$name.png"
        Metadata = Join-Path $destination "$name.json"
    }
    foreach ($path in @($pair.Reference, $pair.Capture, $pair.Metadata)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input; no comparison has been generated: $path" }
    }
    $pair
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
foreach ($pair in $pairs) {
    $mock = $null; $unity = $null; $canvas = $null; $drawing = $null
    $output = Join-Path $destination "compare_$($pair.Name).png"
    try {
        $mock = [System.Drawing.Image]::FromFile($pair.Reference)
        $unity = [System.Drawing.Image]::FromFile($pair.Capture)
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) {
            throw "$($pair.Name) is $($unity.Width)x$($unity.Height). Capture the Game view at 390x844; simulator screenshots belong in the separate simulator folder."
        }
        if ([long]$mock.Width * 844 -ne [long]$mock.Height * 390) {
            throw "$($pair.Name) reference has an unexpected aspect ratio: $($mock.Width)x$($mock.Height)."
        }
        $canvas = [System.Drawing.Bitmap]::new(780, 844)
        $drawing = [System.Drawing.Graphics]::FromImage($canvas)
        $drawing.Clear([System.Drawing.Color]::White)
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $drawing.DrawImage($mock, [System.Drawing.Rectangle]::new(0, 0, 390, 844))
        $drawing.DrawImageUnscaled($unity, 390, 0)
        $canvas.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($drawing) { $drawing.Dispose() }
        if ($canvas) { $canvas.Dispose() }
        if ($mock) { $mock.Dispose() }
        if ($unity) { $unity.Dispose() }
    }
    [PSCustomObject]@{ Name = $pair.Name; Output = $output; Left = $pair.Reference; Right = $pair.Capture }
}
