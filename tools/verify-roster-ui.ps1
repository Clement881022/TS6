param([switch]$SkipBuild, [int]$Width = 1600, [int]$Height = 900)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$copyRoot = Join-Path $projectRoot 'build/ui-layout-client-copy'
$outRoot = Join-Path $projectRoot 'build/roster-ui-win'
$shotsRoot = Join-Path $projectRoot "build/roster-ui-review/${Width}x${Height}"
if (-not $SkipBuild) {
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        & robocopy (Join-Path "$projectRoot/client" $folder) (Join-Path $copyRoot $folder) /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
    }
    $buildLog = Join-Path $projectRoot 'build/roster-ui-build.log'
    $buildArgs = @('-batchmode', '-nographics', '-projectPath', ('"' + $copyRoot + '"'), '-executeMethod', 'SanGuo.EditorTools.SanGuoTools.BuildWindows', '-sanguoOut', ('"' + $outRoot + '"'), '-logFile', ('"' + $buildLog + '"'))
    $build = Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe' -ArgumentList $buildArgs -WindowStyle Hidden -PassThru
    if (-not $build.WaitForExit(600000)) { $build.Kill(); throw 'Unity build timed out' }
    if ($build.ExitCode -ne 0) { throw "Unity build failed: $buildLog" }
}
New-Item -ItemType Directory -Force -Path $shotsRoot | Out-Null
$playerLog = Join-Path $shotsRoot 'player.log'
$playerArgs = @('-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0', '-sanguoLevel', '1', '-sanguoRosterShot', '-sanguoShot', ('"' + $shotsRoot + '"'), '-logFile', ('"' + $playerLog + '"'))
$started = Get-Date
$game = Start-Process (Join-Path $outRoot 'SanGuo.exe') -ArgumentList $playerArgs -WindowStyle Hidden -PassThru
if (-not $game.WaitForExit(120000)) { $game.Kill(); throw "Capture timed out: $playerLog" }
if ($game.ExitCode -ne 0) { throw "Capture failed: $playerLog" }
$shots = @(Get-ChildItem -LiteralPath $shotsRoot -Filter '*.png' | Where-Object { $_.LastWriteTime -ge $started })
if ($shots.Count -ne 18) { throw "Expected 18 captures, got $($shots.Count): $playerLog" }
Add-Type -AssemblyName System.Drawing
foreach ($shot in $shots) {
    $image = [Drawing.Image]::FromFile($shot.FullName)
    try { if ($image.Width -ne $Width -or $image.Height -ne $Height) { throw "Capture dimensions: $($shot.Name)" } }
    finally { $image.Dispose() }
}
$diagnostics = @(Select-String -LiteralPath $playerLog -Pattern 'Exception:|error CS|Failed to parse|USS parsing|Unknown property|Unknown pseudo|warning:')
$baselineWarnings = @($diagnostics | Where-Object { $_.Line -eq 'Unknown pseudo class "last-child" in StyleSheet HomeLayout' })
$errors = @($diagnostics | Where-Object { $_.Line -ne 'Unknown pseudo class "last-child" in StyleSheet HomeLayout' })
if ($errors.Count -gt 0) { throw "Runtime/style diagnostics: $playerLog" }
@{ width = $Width; height = $Height; captureCount = $shots.Count; runtimeErrors = $errors.Count; baselineWarnings = $baselineWarnings.Count; captures = $shots.Name } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $shotsRoot 'capture-manifest.json') -Encoding utf8
$shots | Select-Object Name, Length

