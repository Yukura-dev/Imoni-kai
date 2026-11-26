param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$outputDirectory = Join-Path $ProjectRoot 'docs/validation/m2b'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$pairs = [ordered]@{
    Main_loading='01_Main_loading'; Main_ready='02_Main_ready'; Tutorial1='03_Tutorial1';
    Tutorial2='04_Tutorial2'; Tutorial3='05_Tutorial3'; Home='10_Home';
    Lobby_choose='11_Lobby_choose'; Lobby_join_error='12_Lobby_join_error'; Lobby_team='13_Lobby_team';
    Lobby_ready='14_Lobby_ready'; Waiting='15_Waiting'; Waiting_full='16_Waiting_full';
    Loading='17_Loading'; Loading_done='18_Loading_done'; Member='19_Member'; Member_badges='20_Member_badges';
    Ranking_common='21_Ranking_common'; Ranking_free='22_Ranking_free'; Ranking_endless='23_Ranking_endless'
}
foreach ($name in $pairs.Keys) {
    $mock = [Drawing.Image]::FromFile((Join-Path $ProjectRoot ('design/screenshots/' + $pairs[$name] + '.png')))
    $unity = [Drawing.Image]::FromFile((Join-Path $ProjectRoot ('Library/M2B-' + $name + '.png')))
    try {
        if ($unity.Width -ne 390 -or $unity.Height -ne 844) { throw "Expected 390x844 Unity capture: $name" }
        $bitmap = [Drawing.Bitmap]::new(780, 844)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($mock, [Drawing.Rectangle]::new(0, 0, 390, 844))
            $graphics.DrawImageUnscaled($unity, 390, 0)
            $bitmap.Save((Join-Path $outputDirectory ('compare_' + $name + '.png')), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $bitmap.Dispose() }
    } finally { $mock.Dispose(); $unity.Dispose() }
}
Write-Output "Created $($pairs.Count) comparisons (left mock / right Unity, each 390x844)."
