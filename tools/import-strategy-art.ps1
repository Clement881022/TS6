# Deterministic sprite-atlas import. Sources are generated artwork; cells keep their original alpha.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$skinRoot = Join-Path $projectRoot 'client/Assets/Resources/StrategySkin'
function Split-Atlas([string]$Source, [int]$Columns, [int]$Rows, [string[]]$Names) {
    if ($Names.Count -ne $Columns * $Rows) { throw "Atlas name count mismatch: $Source" }
    $atlas = [Drawing.Bitmap]::new((Join-Path $skinRoot $Source))
    try {
        for ($index = 0; $index -lt $Names.Count; $index++) {
            $column = $index % $Columns
            $row = [int][Math]::Floor($index / $Columns)
            $x0 = [int][Math]::Floor($column * $atlas.Width / $Columns)
            $y0 = [int][Math]::Floor($row * $atlas.Height / $Rows)
            $x1 = [int][Math]::Floor(($column + 1) * $atlas.Width / $Columns)
            $y1 = [int][Math]::Floor(($row + 1) * $atlas.Height / $Rows)
            # The generated roster sheet has nonuniform rows; use its inspected portrait boundaries.
            if ($Source -eq 'atlas-roster.png') {
                $rowEdges = @(0,232,466,697,932,1198,1536)
                $y0 = $rowEdges[$row]
                $y1 = $rowEdges[$row + 1]
            }
            $cellRect = [Drawing.Rectangle]::new($x0, $y0, $x1 - $x0, $y1 - $y0)
            $cell = $atlas.Clone($cellRect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try { $cell.Save((Join-Path $skinRoot ($Names[$index] + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
            finally { $cell.Dispose() }
        }
    } finally { $atlas.Dispose() }
}
if (Test-Path -LiteralPath (Join-Path $skinRoot 'atlas-roster.png')) {
    Split-Atlas 'atlas-roster.png' 3 6 @('face_zhoucang','face_huangfusong','face_huaxiong','face_zhujun','face_handang','face_zoujing','face_zhangbao','face_yuji','face_jianyong','face_luzhi','face_zhangzhongjing','face_ganfuren','face_xiahoudun','face_lvbu','face_gongsunzan','face_zhangjiao','face_xunyu','face_huatuo')
}
Split-Atlas 'atlas-navigation.png' 4 3 @('nav_home','nav_map','nav_heroes','nav_gacha','nav_dungeons','nav_quests','nav_shop','fn_bag','fn_event','back','btn_close','nav_formation')
Split-Atlas 'atlas-items.png' 4 3 @('item_gold','item_yuanbao','item_stamina','item_expbook','item_shard','item_cardmat','item_gift','item_chest','star_on','star_off','lock','unlock')
Split-Atlas 'atlas-combat.png' 6 4 @('damage','heal','armor','hp','cost','charge','pile_draw','pile_discard','draw','status_burn','status_atkup','status_defup','status_armorbreak','status_critup','status_taunt','role_tank','role_warrior','role_archer','role_healer','role_strategist','role_mage','stat_atk','stat_def','stat_int')
if (Test-Path -LiteralPath (Join-Path $skinRoot 'atlas-portraits.png')) {
    Split-Atlas 'atlas-portraits.png' 4 4 @('face_r_sword','face_r_shield','face_r_archer','face_r_healer','face_r_villager','face_yt_archer','face_yt_brute','face_yt_chief','face_yt_ironbrute','face_yt_lieutenant','face_yt_priest','face_yt_sharpshooter','face_yt_soldier','face_r_mage','face_zhangjiao','face_r_strategist')
    $portraitAliases = @{
        'face_r_militia'='face_r_sword'; 'face_yt_warlock'='face_r_mage'; 'face_yt_zhangjiao'='face_zhangjiao';
        'face_bandit_grunt'='face_yt_soldier'; 'face_bandit_archer'='face_yt_archer'; 'face_bandit_marksman'='face_yt_sharpshooter';
        'face_bandit_ironbrute'='face_yt_ironbrute'; 'face_bandit_shaman'='face_r_mage'; 'face_bandit_second'='face_yt_lieutenant';
        'face_bandit_deputy'='face_yt_brute'; 'face_bandit_king'='face_yt_chief';
        'face_tutorial_hunter'='face_r_archer'; 'face_tutorial_scholar'='face_r_strategist'; 'face_tutorial_wanderer'='face_r_sword'
    }
    foreach ($alias in $portraitAliases.Keys) { Copy-Item -LiteralPath (Join-Path $skinRoot ($portraitAliases[$alias] + '.png')) -Destination (Join-Path $skinRoot ($alias + '.png')) -Force }
}
# Hero portrait crops are UI views of the new full illustrations; the complete source art is kept.
$heroCrops = @{
    'guanyu'=@(0.33,0.015,0.40); 'liubei'=@(0.39,0.005,0.42); 'zhangfei'=@(0.28,0.015,0.44);
    'zhaoyun'=@(0.28,0.015,0.44); 'huangzhong'=@(0.30,0.015,0.44); 'pangtong'=@(0.28,0.015,0.44); 'zhugeliang'=@(0.30,0.005,0.46)
}
foreach ($heroName in $heroCrops.Keys) {
    $fullPath = Join-Path $skinRoot ('full_' + $heroName + '.png')
    if (-not (Test-Path -LiteralPath $fullPath)) { continue }
    $full = [Drawing.Bitmap]::new($fullPath)
    try {
        $fractions = $heroCrops[$heroName]
        $side = [int]($full.Width * $fractions[2])
        $portraitRect = [Drawing.Rectangle]::new([int]($full.Width * $fractions[0]), [int]($full.Height * $fractions[1]), $side, $side)
        $face = $full.Clone($portraitRect, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $face.Save((Join-Path $skinRoot ('face_' + $heroName + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $face.Dispose() }
    } finally { $full.Dispose() }
}
foreach ($heroAlias in @('ur_guanyu','ur_zhangfei')) {
    $heroName = $heroAlias.Substring(3)
    foreach ($prefix in @('face_','full_')) {
        if (Test-Path -LiteralPath (Join-Path $skinRoot ($prefix + $heroName + '.png'))) {
            Copy-Item -LiteralPath (Join-Path $skinRoot ($prefix + $heroName + '.png')) -Destination (Join-Path $skinRoot ($prefix + $heroAlias + '.png')) -Force
        }
    }
}
Write-Output 'Imported 48 strategy icons.'
