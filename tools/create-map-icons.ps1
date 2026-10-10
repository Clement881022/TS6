$ErrorActionPreference = 'Stop'
$root = Join-Path (Split-Path -Parent $PSScriptRoot) 'client/Assets/Resources/MapArt'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$icons = @{
    village = '<path d="M18 99V61h42v38zM62 99V48h46v51z" fill="#c7a06b"/><path d="M10 62l29-28 29 28zM53 49l32-29 32 29z" fill="#456d6d"/><path d="M10 62h58M53 49h64" stroke="#f6d891" stroke-width="5"/><path d="M30 99V78h17v21M78 99V72h16v27" fill="#3d3330"/><path d="M19 72h8M51 72h8M66 59h9M96 59h9" stroke="#ffe4a8" stroke-width="5"/><path d="M10 101h108" stroke="#f6d891" stroke-width="5"/>'
    fortress = '<path d="M18 103V61h92v42z" fill="#ae926d"/><path d="M14 62V41h13v10h12V41h13v21M76 62V41h13v10h12V41h13v21" fill="#d2b580"/><path d="M48 62V42h32v20" fill="#c7a06b"/><path d="M40 43l24-20 24 20z" fill="#456d6d"/><path d="M40 43h48M14 63h100M18 104h92" stroke="#f6d891" stroke-width="4"/><path d="M51 104V84a13 13 0 0 1 26 0v20" fill="#352d2b"/><path d="M31 77h9M88 77h9" stroke="#3d3330" stroke-width="7"/><path d="M65 23V8" stroke="#edcf8d" stroke-width="3"/><path d="M67 8h25l-6 7 6 7H67z" fill="#ba5944"/>'
    escort = '<path d="M24 83V48h70v35z" fill="#b78750"/><path d="M18 49q41-51 82 0z" fill="#eee0b6"/><path d="M38 48q21-44 43 0M27 63h63M27 76h63" fill="none" stroke="#785739" stroke-width="4"/><path d="M16 85h94M95 72l17 8" stroke="#edcf8d" stroke-width="5"/><circle cx="37" cy="91" r="14" fill="#3d3330"/><circle cx="84" cy="91" r="14" fill="#3d3330"/><circle cx="37" cy="91" r="10" fill="none" stroke="#edcf8d" stroke-width="4"/><circle cx="84" cy="91" r="10" fill="none" stroke="#edcf8d" stroke-width="4"/><path d="M37 80v22M26 91h22M84 80v22M73 91h22" stroke="#c7a06b" stroke-width="2"/>'
    battle = '<path d="M29 17l21 9 45 57-12 11-46-57z" fill="#c8dad8"/><path d="M29 17l58 71" stroke="#fff0c3" stroke-width="3"/><path d="M72 92l23-21 6 7-23 21z" fill="#edcf8d"/><path d="M86 95l17 18 8-7-17-19z" fill="#b65d43"/><path d="M99 17l-21 9-45 57 12 11 46-57z" fill="#c8dad8"/><path d="M99 17L41 88" stroke="#fff0c3" stroke-width="3"/><path d="M56 92L33 71l-6 7 23 21z" fill="#edcf8d"/><path d="M42 95l-17 18-8-7 17-19z" fill="#b65d43"/>'
    arrow_prev = '<path d="M79 26L41 64l38 38" fill="none" stroke="#edcf8d" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>'
    arrow_next = '<path d="M49 26l38 38-38 38" fill="none" stroke="#edcf8d" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>'
}
$template = Get-Content -LiteralPath (Join-Path $root '../EquipmentArt/armor.svg.meta') -Raw
$template = $template -replace '(?m)[ \t]+(?=\r?$)', ''
foreach ($name in $icons.Keys) {
    $path = Join-Path $root ($name + '.svg')
    $svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><g stroke="#493b2c" stroke-width="2" stroke-linejoin="round">' + $icons[$name] + '</g></svg>'
    [IO.File]::WriteAllText($path, $svg, [Text.UTF8Encoding]::new($false))
    if (-not (Test-Path -LiteralPath ($path + '.meta'))) {
        $meta = $template -replace '(?m)^guid: [a-f0-9]{32}', ('guid: ' + [Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($path + '.meta', $meta, [Text.UTF8Encoding]::new($false))
    }
}
