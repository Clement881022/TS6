param([switch]$SkipBuild, [int]$Width = 1600, [int]$Height = 900, [string]$ReviewName = 'ui-review')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$unityExe = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
$copyRoot = Join-Path $projectRoot 'build/client-copy'
$outRoot = Join-Path $projectRoot 'build/strategy-win'
$shotsRoot = Join-Path $projectRoot "build/$ReviewName/${Width}x${Height}"
if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'sync-core.ps1')
    foreach ($folder in @('Assets','Packages','ProjectSettings')) {
        $sourcePath = Join-Path (Join-Path $projectRoot 'client') $folder
        $destinationPath = Join-Path $copyRoot $folder
        & robocopy $sourcePath $destinationPath /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
    }
    $buildLog = Join-Path $projectRoot 'build/strategy-build.log'
    $unityArguments = @('-batchmode','-nographics','-projectPath', ('"' + $copyRoot + '"'), '-executeMethod','SanGuo.EditorTools.SanGuoTools.BuildWindows','-sanguoOut', ('"' + $outRoot + '"'),'-logFile',('"' + $buildLog + '"'))
    # Wait for the editor itself: Start-Process -Wait also waits for licensing children.
    $buildProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    if (-not $buildProcess.WaitForExit(600000)) { $buildProcess.Kill(); throw "Unity build timed out: $buildLog" }
    if ($buildProcess.ExitCode -ne 0) { throw "Unity build failed: $buildLog" }
}
New-Item -ItemType Directory -Force -Path $shotsRoot | Out-Null
$playerLog = Join-Path $shotsRoot 'player.log'
$gameArguments = @('-screen-width',$Width,'-screen-height',$Height,'-screen-fullscreen','0','-sanguoLevel','1','-sanguoShot',('"' + $shotsRoot + '"'),'-logFile',('"' + $playerLog + '"'))
# The interactive game preview must be visible: minimized swapchains cannot capture the rendered UI.
$game = Start-Process -FilePath (Join-Path $outRoot 'SanGuo.exe') -ArgumentList $gameArguments -WindowStyle Normal -PassThru
$captureStarted = Get-Date
if (-not $game.WaitForExit(60000)) { $game.Kill(); throw 'Screenshot run timed out.' }
if ($game.ExitCode -ne 0) { throw "Screenshot player failed: $($game.ExitCode)" }
$freshShots = @(Get-ChildItem -LiteralPath $shotsRoot -Filter '*.png' | Where-Object { $_.LastWriteTime -ge $captureStarted })
if ($freshShots.Count -ne 31) { throw "Incomplete screenshots: $playerLog" }
Add-Type -AssemblyName System.Drawing
foreach ($shotFile in $freshShots) {
    $shotImage = [Drawing.Image]::FromFile($shotFile.FullName)
    try { if ($shotImage.Width -ne $Width -or $shotImage.Height -ne $Height) { throw "Unexpected screenshot dimensions: $($shotFile.Name)" } }
    finally { $shotImage.Dispose() }
}
$runtimeErrors = @(Select-String -LiteralPath $playerLog -Pattern 'Exception:|NullReferenceException|error CS|ScreenCapture.CaptureScreenshot failed|Failed to parse')
if ($runtimeErrors.Count -gt 0) { throw "Runtime errors found: $playerLog" }
@{ width=$Width; height=$Height; captures=$freshShots.Name; captureCount=$freshShots.Count; runtimeErrors=$runtimeErrors.Count; verifiedAt=(Get-Date).ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $shotsRoot 'capture-manifest.json') -Encoding utf8
$freshShots | Select-Object Name,Length
