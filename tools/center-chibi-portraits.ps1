$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot=Split-Path -Parent $PSScriptRoot
$skin=Join-Path $projectRoot 'client/Assets/Resources/ChibiSkin'
$frames=Get-Content (Join-Path $projectRoot 'docs/art-chibi/portrait-framing.json') -Raw | ConvertFrom-Json
foreach($entry in $frames.PSObject.Properties) {
    $id=$entry.Name; $focus=$entry.Value
    $image=[Drawing.Bitmap]::new((Join-Path $skin ('full_'+$id+'.png')))
    try {
        foreach($kind in @('face','bust')) {
            $width=768; $height=if($kind -eq 'face'){768}else{960}
            $window=$image.Width*$focus[2]*( $(if($kind -eq 'face'){1.0}else{1.5}) )
            $scale=$width/$window
            $eyeTargetY=if($kind -eq 'face'){.52}else{.30}
            $left=$width*.5-$image.Width*$focus[0]*$scale
            $top=$height*$eyeTargetY-$image.Height*$focus[1]*$scale
            $canvas=[Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics=[Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.Clear([Drawing.Color]::Transparent)
                $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.DrawImage($image,[Drawing.RectangleF]::new($left,$top,$image.Width*$scale,$image.Height*$scale))
                $canvas.Save((Join-Path $skin ($kind+'_'+$id+'.png')),[Drawing.Imaging.ImageFormat]::Png)
            } finally { $graphics.Dispose(); $canvas.Dispose() }
        }
    } finally { $image.Dispose() }
}
foreach($id in @('guanyu','zhangfei')) {
    foreach($kind in @('face','bust')) { Copy-Item -LiteralPath (Join-Path $skin ($kind+'_'+$id+'.png')) -Destination (Join-Path $skin ($kind+'_ur_'+$id+'.png')) -Force }
}
Write-Output 'Centred independent face and bust compositions generated.'
