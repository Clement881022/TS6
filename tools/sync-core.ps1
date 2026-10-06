# 建置 SanGuo.Core 並複製 DLL 到 Unity 專案（Assets/Plugins/SanGuo）。
# 修改 src/SanGuo.Core 後執行此腳本，再回到 Unity 等待重新編譯。
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root\src\SanGuo.Core\SanGuo.Core.csproj" -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Core 建置失敗" }
$dest = "$root\client\Assets\Plugins\SanGuo"
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$root\src\SanGuo.Core\bin\Release\netstandard2.1\SanGuo.Core.dll" $dest -Force
Copy-Item "$root\src\SanGuo.Core\bin\Release\netstandard2.1\SanGuo.Core.pdb" $dest -Force -ErrorAction SilentlyContinue
Write-Host "已同步到 $dest"
