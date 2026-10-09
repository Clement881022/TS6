param([switch]$SkipBuild, [int]$Width = 1600, [int]$Height = 900, [int]$Level = 1)
$root = Split-Path -Parent $PSScriptRoot
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe"
$copy = "$root\build\client-copy"
$shots = "$root\build\shots"
New-Item -ItemType Directory -Force "$root\build" | Out-Null

if (-not $SkipBuild) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\sync-core.ps1"
    foreach ($dir in "Assets", "Packages", "ProjectSettings") {
        robocopy "$root\client\$dir" "$copy\$dir" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "複製 $dir 失敗" }
    }
    $p = Start-Process $unity -ArgumentList @("-batchmode","-nographics","-projectPath",$copy,
        "-executeMethod","SanGuo.EditorTools.SanGuoTools.BuildWindows","-sanguoOut","$root\build\win","-logFile","$root\build\unity-build.log") -PassThru -Wait
    if ($p.ExitCode -ne 0) { throw "Unity 打包失敗，請看 build\unity-build.log" }
}

$exe = "$root\build\win\SanGuo.exe"
Remove-Item $shots -Recurse -Force -ErrorAction SilentlyContinue
$game = Start-Process $exe -ArgumentList @("-screen-width",$Width,"-screen-height",$Height,
    "-screen-fullscreen","0","-sanguoLevel",$Level,"-sanguoShot",$shots) -PassThru
if (-not $game.WaitForExit(60000)) { $game.Kill(); throw "截圖逾時" }
Get-ChildItem $shots | Select-Object Name, Length
