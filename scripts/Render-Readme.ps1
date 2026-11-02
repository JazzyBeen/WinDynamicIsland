$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root 'local\screenshots'
$target=Join-Path $root 'assets\screenshots'
New-Item -ItemType Directory -Path $source,$target -Force | Out-Null
$exe=Join-Path $root 'artifacts\app\WinDynamicIsland.exe'
foreach($mode in @('--render-settings','--render-features','--render-focus')){
    Start-Process -FilePath $exe -ArgumentList @($mode,('"'+$source+'"')) -WindowStyle Hidden -Wait
}
Add-Type -AssemblyName System.Drawing
foreach($pair in @(@('Focus-ru.png','settings-focus.png'),@('Appearance-ru.png','settings-appearance.png'))){
    $image=[Drawing.Image]::FromFile((Join-Path $source $pair[0]))
    try{
        $crop=New-Object Drawing.Rectangle 8,38,980,740
        $client=New-Object Drawing.Bitmap 980,740
        $g=[Drawing.Graphics]::FromImage($client)
        $g.DrawImage($image,(New-Object Drawing.Rectangle 0,0,980,740),$crop,[Drawing.GraphicsUnit]::Pixel)
        $g.Dispose();$client.Save((Join-Path $target $pair[1]));$client.Dispose()
    }finally{$image.Dispose()}
}
$canvas=New-Object Drawing.Bitmap 1600,900
$g=[Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$bg=New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Point]::new(0,0)),([Drawing.Point]::new(1600,900)),([Drawing.Color]::FromArgb(12,14,24)),([Drawing.Color]::FromArgb(22,17,39))
$g.FillRectangle($bg,0,0,1600,900);$bg.Dispose()
$small=New-Object Drawing.Font 'Segoe UI',16
$title=New-Object Drawing.Font 'Segoe UI',64,([Drawing.FontStyle]::Bold)
$subtitle=New-Object Drawing.Font 'Segoe UI',23
$muted=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(162,168,190))
$g.DrawString('WIN DYNAMIC ISLAND',$small,$muted,100,72)
$g.DrawString('A calmer Windows.',$title,[Drawing.Brushes]::White,90,132)
$g.DrawString('Stay focused. Keep everything within reach.',$subtitle,$muted,99,256)
foreach($item in @(@('media-expanded.png',100,390,600,264),@('focus-expanded.png',860,390,600,264),@('focus-compact.png',1100,310,186,60),@('charging.png',937,720,446,50),@('headphones.png',197,720,390,60))){
    $image=[Drawing.Image]::FromFile((Join-Path $source $item[0]))
    $g.DrawImage($image,[int]$item[1],[int]$item[2],[int]$item[3],[int]$item[4]);$image.Dispose()
}
$g.DrawString('MEDIA  /  FOCUS  /  NOTIFICATIONS  /  DEVICES',$small,$muted,100,826)
$canvas.Save((Join-Path $target 'hero.png'))
$g.Dispose();$canvas.Dispose();$small.Dispose();$title.Dispose();$subtitle.Dispose();$muted.Dispose()
