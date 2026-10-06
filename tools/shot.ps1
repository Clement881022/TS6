# 打包 Windows 測試版並自動操作截圖，輸出到 build/shots。
# 用法：powershell -File tools/shot.ps1   （-SkipBuild 可略過打包；-Width/-Height 指定視窗大小；-Level 指定關卡）
#
# 為了讓你開著 Unity 編輯器時也能驗證（同一個專案不能被兩個 Unity 同時開啟），
# 這裡會把專案複製到 build/client-copy 再打包；該副本的 Library 會保留，之後只做增量編譯。
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
