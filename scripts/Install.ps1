param([string]$Source,[switch]$MigrateLegacy,[switch]$NoLaunch,[switch]$ForceRestart)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Source){$Source=Join-Path $root 'app';if(-not(Test-Path -LiteralPath $Source)){$Source=Join-Path $root 'artifacts\app'}}
$Source=(Resolve-Path -LiteralPath $Source).Path
if(-not(Test-Path -LiteralPath (Join-Path $Source 'WinDynamicIsland.exe'))){throw 'Build the app first: scripts\Build.ps1'}
$install=Join-Path $env:LOCALAPPDATA 'Programs\WinDynamicIsland'
$data=Join-Path $env:LOCALAPPDATA 'WinDynamicIsland'
New-Item -ItemType Directory -Path $install,$data -Force | Out-Null
$current=Join-Path $install 'WinDynamicIsland.exe'
Get-Process WinDynamicIsland -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $current} | ForEach-Object {
    Start-Process -FilePath $current -ArgumentList '--exit' -WindowStyle Hidden -Wait
    if(-not $_.WaitForExit(12000)){if($ForceRestart -and $_.Path -eq $current){Stop-Process -InputObject $_ -Force}else{throw 'Close Win Dynamic Island and run setup again'}}
}
Get-Process IslandGuard -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $install 'IslandGuard.exe')} | Stop-Process
if($MigrateLegacy){
    $legacy=Join-Path $env:LOCALAPPDATA 'SotaVPN-Toggle'
    foreach($name in @('api-profile.bin','browser-loopback-token.txt','telegram-banner-backup.json','codex-com.openai.codex-banner-backup.json','codex-OpenAI.Codex_2p2nqsd0c76g0!App-banner-backup.json','system-Windows.SystemToast.Power-banner-backup.json','battery-warning-backup.json')){
        $from=Join-Path $legacy $name;$to=Join-Path $data $name
        if((Test-Path -LiteralPath $from) -and -not(Test-Path -LiteralPath $to)){Copy-Item -LiteralPath $from -Destination $to}
    }
    $config=Join-Path $data 'settings.json'
    if(-not(Test-Path -LiteralPath $config)){
        $settings=@{SchemaVersion=1;Language='ru';StartWithWindows=$true;ShowStartupIsland=$true;VpnEnabled=$false;VpnProvider='None';VpnName='VPN'}
        if(Test-Path -LiteralPath (Join-Path $data 'api-profile.bin')){$settings.VpnEnabled=$true;$settings.VpnProvider='Sota';$settings.VpnName='Sota VPN'}
        $settings.TelegramEnabled=-not(Test-Path -LiteralPath (Join-Path $legacy 'telegram-disabled.flag'))
        $settings.CodexEnabled=-not(Test-Path -LiteralPath (Join-Path $legacy 'codex-disabled.flag'))
        $settings.BatteryEnabled=-not(Test-Path -LiteralPath (Join-Path $legacy 'system-disabled.flag'))
        $settings | ConvertTo-Json | Set-Content -LiteralPath $config -Encoding UTF8
    }
}
foreach($name in @('WinDynamicIsland.exe','IslandGuard.exe')){Copy-Item -LiteralPath (Join-Path $Source $name) -Destination $install -Force}
if(Test-Path -LiteralPath (Join-Path $Source 'licenses')){Copy-Item (Join-Path $Source 'licenses') $install -Recurse -Force}
Copy-Item (Join-Path $Source 'WinDynamicIsland.Core.dll') $install -Force
Copy-Item (Join-Path $Source 'MaterialColorUtilities.dll') $install -Force
Copy-Item (Join-Path $Source 'codex-icon.png') $data -Force
# Packages ship app\Music; a source build keeps the pack in artifacts\music-pack.
$music=Join-Path $Source 'Music'
if(-not(Test-Path -LiteralPath $music)){$music=Join-Path $root 'artifacts\music-pack\Music'}
if(Test-Path -LiteralPath $music){
    New-Item -ItemType Directory -Path (Join-Path $data 'Music') -Force | Out-Null
    Copy-Item (Join-Path $music '*') (Join-Path $data 'Music') -Recurse -Force
}else{Write-Warning 'Focus music is not installed: run scripts\Download-Music.ps1'}
$extension=Join-Path $install 'browser-extension';New-Item -ItemType Directory -Path $extension -Force | Out-Null
Get-ChildItem (Join-Path $Source 'browser-extension') -File | Where-Object {$_.Name -ne 'connection.js'} | Copy-Item -Destination $extension -Force
$shell=New-Object -ComObject WScript.Shell
$links=@((Join-Path ([Environment]::GetFolderPath('Programs')) 'Win Dynamic Island.lnk'),(Join-Path ([Environment]::GetFolderPath('Desktop')) 'Win Dynamic Island.lnk'))
foreach($path in $links){$link=$shell.CreateShortcut($path);$link.TargetPath=$current;$link.Arguments='--settings';$link.WorkingDirectory=$install;$link.IconLocation=$current+',0';$link.Save()}
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$settingsPath=Join-Path $data 'settings.json'
$config=if(Test-Path -LiteralPath $settingsPath){Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json}else{$null}
if($null -eq $config -or $config.StartWithWindows){New-ItemProperty -Path $run -Name WinDynamicIsland -Value ('"'+$current+'" --background') -PropertyType String -Force | Out-Null}
else{Remove-ItemProperty -Path $run -Name WinDynamicIsland -ErrorAction SilentlyContinue}
if(-not $NoLaunch){Start-Process -FilePath $current -ArgumentList '--settings' -WindowStyle Hidden}
Write-Output ('Installed: '+$current)
