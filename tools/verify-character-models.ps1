param([switch]$SkipBuild,[switch]$SkipSync,[string]$Models='guanyu,r_shield,r_archer,r_healer,bandit_grunt',[string]$ReviewName='model-quality/stage3')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$copyRoot=Join-Path $projectRoot 'build/character-copy'
$outRoot=Join-Path $projectRoot 'build/character-win'
$shotsRoot=Join-Path $projectRoot ('build/'+$ReviewName)
$unityExe='C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe'
if(-not $SkipBuild) {
    if(-not $SkipSync){ foreach($folder in @('Assets','Packages','ProjectSettings')) {
        & robocopy (Join-Path (Join-Path $projectRoot 'client') $folder) (Join-Path $copyRoot $folder) /E /NFL /NDL /NJH /NJS /NP | Out-Null
        if($LASTEXITCODE -ge 8){throw "Copy failed: $folder"}
    } }
    $log=Join-Path $projectRoot 'build/model-quality/character-build.log'
    $p=Start-Process -FilePath $unityExe -ArgumentList @('-batchmode','-nographics','-projectPath',('"'+$copyRoot+'"'),'-executeMethod','SanGuo.EditorTools.SanGuoTools.BuildWindows','-sanguoOut',('"'+$outRoot+'"'),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    if(-not $p.WaitForExit(600000)){throw "Build timed out: $log"}
    if($p.ExitCode -ne 0){throw "Build failed: $log"}
}
New-Item -ItemType Directory -Force -Path $shotsRoot | Out-Null
$playerLog=Join-Path $shotsRoot 'player.log'
$started=Get-Date
$p=Start-Process -FilePath (Join-Path $outRoot 'SanGuo.exe') -ArgumentList @('-screen-width','1600','-screen-height','900','-screen-fullscreen','0','-sanguoShot',('"'+$shotsRoot+'"'),'-sanguoModelReview',$Models,'-logFile',('"'+$playerLog+'"')) -WindowStyle Normal -PassThru
if(-not $p.WaitForExit(60000)){ $p.Kill();throw 'Model capture timed out.' }
if($p.ExitCode -ne 0){throw 'Model capture failed.'}
$shots=@(Get-ChildItem -LiteralPath $shotsRoot -Filter '*.png' | Where-Object {$_.LastWriteTime -ge $started})
$expected=$Models.Split(',').Count*4
if($shots.Count -ne $expected){throw "Expected $expected fresh captures; found $($shots.Count)."}
$errors=@(Select-String -LiteralPath $playerLog -Pattern 'Exception:|NullReferenceException|error CS|Shader error')
if($errors.Count -gt 0){throw "Runtime errors: $playerLog"}
@{models=$Models.Split(',');captures=$shots.Name;captureCount=$shots.Count;runtimeErrors=$errors.Count;verifiedAt=(Get-Date).ToString('o');visualApproval=$false} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $shotsRoot 'capture-manifest.json') -Encoding utf8
$shots | Select-Object Name,Length
