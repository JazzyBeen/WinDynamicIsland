param([switch]$IncludeMusic)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1')
$suffix=if($IncludeMusic){'-with-music'}else{''}
$package=Join-Path $root ('artifacts\WinDynamicIsland-2.2.1'+$suffix)
New-Item -ItemType Directory -Path (Join-Path $package 'app'),(Join-Path $package 'scripts') -Force | Out-Null
Copy-Item (Join-Path $root 'artifacts\app\WinDynamicIsland.exe'),(Join-Path $root 'artifacts\app\IslandGuard.exe'),(Join-Path $root 'artifacts\app\WinDynamicIsland.Core.dll'),(Join-Path $root 'artifacts\app\codex-icon.png') (Join-Path $package 'app') -Force
Copy-Item (Join-Path $root 'artifacts\app\licenses') (Join-Path $package 'app') -Recurse -Force
Copy-Item (Join-Path $root 'artifacts\app\browser-extension') (Join-Path $package 'app') -Recurse -Force
if($IncludeMusic){
    $music=Join-Path $root 'artifacts\music-pack\Music'
    if(-not(Test-Path -LiteralPath $music)){throw 'Download the music pack first with scripts/Download-Music.ps1 -Destination artifacts/music-pack/Music'}
    Copy-Item $music (Join-Path $package 'app') -Recurse -Force
}
Copy-Item (Join-Path $root 'README.md') $package -Force
Copy-Item (Join-Path $PSScriptRoot 'Install.ps1') (Join-Path $package 'scripts') -Force
Copy-Item (Join-Path $root 'Install.cmd') $package -Force
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $root ('artifacts\WinDynamicIsland-2.2.1'+$suffix+'.zip')) -Force
Write-Output ('Package: '+(Join-Path $root ('artifacts\WinDynamicIsland-2.2.1'+$suffix+'.zip')))
