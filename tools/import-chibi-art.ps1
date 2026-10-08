# Deterministic atlas slicing and portrait views of the approved Q-style source art.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$skinRoot = Join-Path $projectRoot 'client/Assets/Resources/ChibiSkin'
function Split-ChibiAtlas([string]$Source, [int]$Columns, [int]$Rows, [string[]]$Names) {
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
            $cell = $atlas.Clone([Drawing.Rectangle]::new($x0, $y0, $x1 - $x0, $y1 - $y0), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try { $cell.Save((Join-Path $skinRoot ($Names[$index] + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
            finally { $cell.Dispose() }
        }
    } finally { $atlas.Dispose() }
}
Split-ChibiAtlas 'atlas-navigation.png' 4 3 @('nav_home','nav_map','nav_heroes','nav_gacha','nav_dungeons','nav_quests','nav_shop','fn_bag','fn_event','back','btn_close','nav_formation')
Split-ChibiAtlas 'atlas-items.png' 4 3 @('item_gold','item_yuanbao','item_stamina','item_expbook','item_shard','item_cardmat','item_gift','item_chest','star_on','star_off','lock','unlock')
Split-ChibiAtlas 'atlas-combat.png' 6 4 @('damage','heal','armor','hp','cost','charge','pile_draw','pile_discard','draw','status_burn','status_atkup','status_defup','status_armorbreak','status_critup','status_taunt','role_tank','role_warrior','role_archer','role_healer','role_strategist','role_mage','stat_atk','stat_def','stat_int')
Split-ChibiAtlas 'atlas-roster.png' 6 3 @('face_zhoucang','face_huangfusong','face_huaxiong','face_zhujun','face_handang','face_zoujing','face_zhangbao','face_yuji','face_jianyong','face_luzhi','face_zhangzhongjing','face_ganfuren','face_xiahoudun','face_lvbu','face_gongsunzan','face_zhangjiao','face_xunyu','face_huatuo')
Split-ChibiAtlas 'atlas-portraits.png' 4 4 @('face_r_sword','face_r_shield','face_r_archer','face_r_healer','face_r_villager','face_yt_archer','face_yt_brute','face_yt_chief','face_yt_ironbrute','face_yt_lieutenant','face_yt_priest','face_yt_sharpshooter','face_yt_soldier','face_r_mage','face_yt_zhangjiao','face_r_strategist')
Split-ChibiAtlas 'atlas-full-sr.png' 4 3 @('full_zhoucang','full_huangfusong','full_huaxiong','full_zhujun','full_handang','full_zoujing','full_zhangbao','full_yuji','full_jianyong','full_luzhi','full_zhangzhongjing','full_ganfuren')
# The militia source puts the swordsman before the shield soldier.
Split-ChibiAtlas 'atlas-full-militia.png' 3 2 @('full_r_sword','full_r_shield','full_r_archer','full_r_mage','full_r_healer','full_r_strategist')
$portraitAliases = @{
    'face_r_militia'='face_r_sword'; 'face_yt_warlock'='face_r_mage';
    'face_bandit_grunt'='face_yt_soldier'; 'face_bandit_archer'='face_yt_archer'; 'face_bandit_marksman'='face_yt_sharpshooter';
    'face_bandit_ironbrute'='face_yt_ironbrute'; 'face_bandit_shaman'='face_r_mage'; 'face_bandit_second'='face_yt_lieutenant';
    'face_bandit_deputy'='face_yt_brute'; 'face_bandit_king'='face_yt_chief';
    'face_tutorial_hunter'='face_r_archer'; 'face_tutorial_scholar'='face_r_strategist'; 'face_tutorial_wanderer'='face_r_sword'
}
foreach ($alias in $portraitAliases.Keys) { Copy-Item -LiteralPath (Join-Path $skinRoot ($portraitAliases[$alias] + '.png')) -Destination (Join-Path $skinRoot ($alias + '.png')) -Force }
# Head framing follows each inspected full illustration rather than the old realistic crops.
$heroCrops = @{
    'guanyu'=@(0.44,0.025,0.40); 'liubei'=@(0.36,0.00,0.46); 'zhangfei'=@(0.40,0.015,0.40);
    'zhaoyun'=@(0.40,0.005,0.40); 'huangzhong'=@(0.33,0.03,0.45); 'pangtong'=@(0.41,0.00,0.44); 'zhugeliang'=@(0.23,0.005,0.47);
    'xiahoudun'=@(0.32,0.04,0.37); 'lvbu'=@(0.40,0.34,0.38); 'gongsunzan'=@(0.26,0.10,0.37);
    'zhangjiao'=@(0.32,0.055,0.45); 'xunyu'=@(0.36,0.00,0.47); 'huatuo'=@(0.25,0.005,0.48)
}
foreach ($heroName in $heroCrops.Keys) {
    $full = [Drawing.Bitmap]::new((Join-Path $skinRoot ('full_' + $heroName + '.png')))
    try {
        $fractions = $heroCrops[$heroName]
        $side = [int]($full.Width * $fractions[2])
        $face = $full.Clone([Drawing.Rectangle]::new([int]($full.Width * $fractions[0]), [int]($full.Height * $fractions[1]), $side, $side), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $face.Save((Join-Path $skinRoot ('face_' + $heroName + '.png')), [Drawing.Imaging.ImageFormat]::Png) }
        finally { $face.Dispose() }
    } finally { $full.Dispose() }
}
foreach ($heroAlias in @('ur_guanyu','ur_zhangfei')) {
    $heroName = $heroAlias.Substring(3)
    foreach ($prefix in @('face_','full_')) { Copy-Item -LiteralPath (Join-Path $skinRoot ($prefix + $heroName + '.png')) -Destination (Join-Path $skinRoot ($prefix + $heroAlias + '.png')) -Force }
}
Write-Output 'Imported Q-style icons, portraits and hero views.'
& (Join-Path $PSScriptRoot 'center-chibi-portraits.ps1')
