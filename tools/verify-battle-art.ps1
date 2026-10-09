param([string]$ReviewName='model-quality/battle-art', [string]$Resolutions='1920x1080,1366x768,1280x1024')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$player=Join-Path $projectRoot 'build/character-win/SanGuo.exe'
if(-not (Test-Path -LiteralPath $player)){throw 'Build the isolated character verification player first.'}
foreach($resolution in $Resolutions.Split(',')) {
    if($resolution -notmatch '^(\d+)x(\d+)$'){throw "Invalid resolution: $resolution"}
    $pixelWidth=$Matches[1];$pixelHeight=$Matches[2]
    $shotsRoot=Join-Path $projectRoot ('build/'+$ReviewName+'/'+$resolution)
    New-Item -ItemType Directory -Force -Path $shotsRoot | Out-Null
    $playerLog=Join-Path $shotsRoot 'player.log'
    $started=Get-Date
    $p=Start-Process -FilePath $player -ArgumentList @('-screen-width',$pixelWidth,'-screen-height',$pixelHeight,'-screen-fullscreen','0','-sanguoLevel','1','-sanguoShot',('"'+$shotsRoot+'"'),'-logFile',('"'+$playerLog+'"')) -WindowStyle Normal -PassThru
    if(-not $p.WaitForExit(60000)){throw "Capture timed out: $resolution"}
    if($p.ExitCode -ne 0){throw "Capture failed: $resolution"}
    $shots=@(Get-ChildItem -LiteralPath $shotsRoot -Filter '*.png' | Where-Object {$_.LastWriteTime -ge $started})
    if($shots.Count -ne 34){throw "Expected 34 fresh captures at $resolution; found $($shots.Count)."}
    $errors=@(Select-String -LiteralPath $playerLog -Pattern 'Exception:|error CS|Shader error')
    if($errors.Count -gt 0){throw "Runtime errors: $playerLog"}
    $gridChecks=@(Select-String -LiteralPath $playerLog -SimpleMatch '[shot] Battle grid center raycasts verified: 25')
    $layoutChecks=@(Select-String -LiteralPath $playerLog -SimpleMatch '[shot] Battle HUD containment, separation and card text fit verified.')
    if($gridChecks.Count -lt 10 -or $layoutChecks.Count -lt 10){throw "Missing battle validation checks: $resolution"}
    @{resolution=$resolution;captureCount=$shots.Count;runtimeErrors=$errors.Count;gridChecks=$gridChecks.Count;layoutChecks=$layoutChecks.Count;verifiedAt=(Get-Date).ToString('o');visualApproval=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $shotsRoot 'capture-manifest.json') -Encoding utf8
    Write-Output "$resolution : $($shots.Count) captures, $($gridChecks.Count) grid checks, $($layoutChecks.Count) layout checks, 0 runtime errors. Visual review remains required."
}
