param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
      [string]$Ffmpeg = 'ffmpeg', [switch]$Videos)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m5'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$screens = @('40_Imoni_start', '41_Imoni_mid', '42_Imoni_finale', '44_ImoniFail_mid', '45_ImoniFail_end', '43_Result', '46_GameOver')
foreach ($screen in $screens) {
    $mock = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "design/screenshots/$screen.png"))
    $unity = [System.Drawing.Image]::FromFile((Join-Path $ProjectRoot "Library/M5-$screen.png"))
    $canvas = [System.Drawing.Bitmap]::new(780, 844)
    $drawing = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) { throw "Unexpected capture dimensions: $screen" }
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.DrawImage($mock, [System.Drawing.Rectangle]::new(0, 0, 390, 844))
        $drawing.DrawImage($unity, [System.Drawing.Rectangle]::new(390, 0, 390, 844))
        $canvas.Save((Join-Path $destination "compare_$screen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $drawing.Dispose(); $canvas.Dispose(); $mock.Dispose(); $unity.Dispose() }
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M5-$screen.png") -Destination (Join-Path $destination "unity_$screen.png")
    Copy-Item -LiteralPath (Join-Path $ProjectRoot "Library/M5-$screen.json") -Destination (Join-Path $destination "state_$screen.json")
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $ProjectRoot 'Library') -File -Filter 'M5-proof_*') {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name.Substring(3))
}
if ($Videos) {
    foreach ($name in @('imoni_success', 'imoni_fail')) {
        $frameDirectory = Join-Path $ProjectRoot "Library/M5-$name-frames"
        $telemetry = Get-Content -LiteralPath "$frameDirectory.json" -Raw | ConvertFrom-Json
        $output = Join-Path $destination "unity_$name.mp4"
        & $Ffmpeg -y -hide_banner -loglevel error -framerate $telemetry.frameRate -i (Join-Path $frameDirectory '%05d.png') -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -movflags +faststart $output
        if ($LASTEXITCODE -ne 0) { throw "Encoding failed: $name" }
        Copy-Item -LiteralPath "$frameDirectory.json" -Destination (Join-Path $destination "$name-telemetry.json")
        & $Ffmpeg -y -hide_banner -loglevel error -i (Join-Path $ProjectRoot "design/videos/$name.mp4") -i $output -filter_complex '[0:v][1:v]hstack=inputs=2:shortest=1[v]' -map '[v]' -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart (Join-Path $destination "compare_$name.mp4")
        if ($LASTEXITCODE -ne 0) { throw "Comparison encoding failed: $name" }
        # First non-blank frame measured at 25 fps in the provided reference videos.
        $referenceIntro = if ($name -eq 'imoni_success') { '0.76' } else { '0.24' }
        & $Ffmpeg -y -hide_banner -loglevel error -ss $referenceIntro -i (Join-Path $ProjectRoot "design/videos/$name.mp4") -i $output -filter_complex '[0:v]setpts=PTS-STARTPTS[left];[1:v]setpts=PTS-STARTPTS[right];[left][right]hstack=inputs=2:shortest=1[v]' -map '[v]' -c:v libx264 -crf 20 -pix_fmt yuv420p -movflags +faststart (Join-Path $destination "compare_aligned_$name.mp4")
        if ($LASTEXITCODE -ne 0) { throw "Aligned comparison encoding failed: $name" }
    }
}
Write-Output "Saved seven comparisons and validation evidence to $destination"
