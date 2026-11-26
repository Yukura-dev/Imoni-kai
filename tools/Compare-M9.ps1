param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $ProjectRoot 'docs/validation/m9'
[IO.Directory]::CreateDirectory($destination) | Out-Null
$names = @('50_Help_1_controls','51_Help_2_flow','52_Help_3_store','53_Help_4_upgrade','54_Help_5_score','10_Home','04_Tutorial2','05_Tutorial3','34_GameMid_menu')
foreach ($name in $names) {
    $source = Join-Path $ProjectRoot ('Library/M2B-m9-' + $name + '.png')
    $left = [Drawing.Image]::FromFile((Join-Path $ProjectRoot ('design/screenshots/' + $name + '.png')))
    $right = [Drawing.Image]::FromFile($source)
    try {
        if ($right.Width -ne 390 -or $right.Height -ne 844) { throw 'Expected Unity capture at 390x844.' }
        $canvas = [Drawing.Bitmap]::new(780,844)
        $drawing = [Drawing.Graphics]::FromImage($canvas)
        try {
            $drawing.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $drawing.DrawImage($left,[Drawing.Rectangle]::new(0,0,390,844))
            $drawing.DrawImageUnscaled($right,390,0)
            $canvas.Save((Join-Path $destination ('compare_' + $name + '.png')),[Drawing.Imaging.ImageFormat]::Png)
        } finally { $drawing.Dispose(); $canvas.Dispose() }
    } finally { $left.Dispose(); $right.Dispose() }
    Copy-Item -LiteralPath $source -Destination (Join-Path $destination ('unity_' + $name + '.png')) -Force
    Write-Output $name
}
