[CmdletBinding()]
param(
    [string[]]$Devices,
    [string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent),
    [ValidateRange(1, 20)][int]$Columns = 5,
    [ValidateRange(1, 4096)][int]$ThumbnailWidth = 156,
    [ValidateRange(1, 4096)][int]$ThumbnailHeight = 337
)

# Copy original captures to docs/validation/m8/simulator/<device> before running.
# Example: ./tools/Build-M8ContactSheet.ps1 -Devices 'iPhone12','OnePlus6T'
# The sheets fit the whole image into each thumbnail; originals are never resized or replaced.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$sourceRoot = Join-Path $ProjectRoot 'docs/validation/m8/simulator'
if (-not (Test-Path -LiteralPath $sourceRoot -PathType Container)) { throw "Missing simulator capture folder: $sourceRoot" }
if (-not $Devices -or $Devices.Count -eq 0) {
    $Devices = @(Get-ChildItem -LiteralPath $sourceRoot -Directory | Sort-Object Name | Select-Object -ExpandProperty Name)
}
if ($Devices.Count -eq 0) { throw 'No device capture folders were found.' }

$groups = foreach ($device in ($Devices | Select-Object -Unique)) {
    if ($device -in @('.', '..') -or $device -match '[\\/:*?"<>|]') { throw "Invalid device folder name: $device" }
    $folder = Join-Path $sourceRoot $device
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "Missing device folder: $folder" }
    $files = @(Get-ChildItem -LiteralPath $folder -File -Filter '*.png' | Sort-Object Name)
    if ($files.Count -eq 0) { throw "No PNG captures in $folder" }
    [PSCustomObject]@{ Device = $device; Files = $files }
}

$labelHeight = 24
$headerHeight = 36
foreach ($group in $groups) {
    $rows = [int][Math]::Ceiling($group.Files.Count / [double]$Columns)
    $width = $Columns * $ThumbnailWidth
    $height = $headerHeight + $rows * ($ThumbnailHeight + $labelHeight)
    $canvas = $null; $drawing = $null; $headerFont = $null; $labelFont = $null; $labelFormat = $null
    $output = Join-Path $sourceRoot "contact_$($group.Device).png"
    try {
        $canvas = [System.Drawing.Bitmap]::new($width, $height)
        $drawing = [System.Drawing.Graphics]::FromImage($canvas)
        $drawing.Clear([System.Drawing.Color]::White)
        $drawing.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $drawing.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $headerFont = [System.Drawing.Font]::new('Yu Gothic UI', 14, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $labelFont = [System.Drawing.Font]::new('Yu Gothic UI', 10, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
        $labelFormat = [System.Drawing.StringFormat]::new()
        $labelFormat.Alignment = [System.Drawing.StringAlignment]::Center
        $labelFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
        $labelFormat.Trimming = [System.Drawing.StringTrimming]::EllipsisCharacter
        $labelFormat.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
        $drawing.DrawString("$($group.Device) - $($group.Files.Count) captures", $headerFont, [System.Drawing.Brushes]::Black,
            [System.Drawing.RectangleF]::new(0, 0, $width, $headerHeight), $labelFormat)
        for ($index = 0; $index -lt $group.Files.Count; $index++) {
            $file = $group.Files[$index]
            $left = ($index % $Columns) * $ThumbnailWidth
            $top = $headerHeight + [int][Math]::Floor($index / [double]$Columns) * ($ThumbnailHeight + $labelHeight)
            $capture = $null
            try {
                $capture = [System.Drawing.Image]::FromFile($file.FullName)
                $scale = [Math]::Min($ThumbnailWidth / [double]$capture.Width, $ThumbnailHeight / [double]$capture.Height)
                $imageWidth = [single]($capture.Width * $scale)
                $imageHeight = [single]($capture.Height * $scale)
                $rectangle = [System.Drawing.RectangleF]::new(
                    [single]($left + ($ThumbnailWidth - $imageWidth) / 2),
                    [single]($top + ($ThumbnailHeight - $imageHeight) / 2), $imageWidth, $imageHeight)
                $drawing.DrawImage($capture, $rectangle)
            }
            finally { if ($capture) { $capture.Dispose() } }
            $drawing.DrawString($file.BaseName, $labelFont, [System.Drawing.Brushes]::Black,
                [System.Drawing.RectangleF]::new($left, $top + $ThumbnailHeight, $ThumbnailWidth, $labelHeight), $labelFormat)
        }
        $canvas.Save($output, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        if ($labelFormat) { $labelFormat.Dispose() }
        if ($labelFont) { $labelFont.Dispose() }
        if ($headerFont) { $headerFont.Dispose() }
        if ($drawing) { $drawing.Dispose() }
        if ($canvas) { $canvas.Dispose() }
    }
    [PSCustomObject]@{ Device = $group.Device; Images = $group.Files.Count; Width = $width; Height = $height; Output = $output }
}
