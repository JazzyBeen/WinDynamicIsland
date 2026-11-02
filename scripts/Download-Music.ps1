param([string]$Archive, [string]$Destination)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Destination){$Destination=Join-Path $env:LOCALAPPDATA 'WinDynamicIsland\Music'}
$catalog=Get-Content (Join-Path $root 'assets\music\catalog.json') -Raw | ConvertFrom-Json
$temporary=$false
if(-not $Archive){
    $Archive=Join-Path ([IO.Path]::GetTempPath()) ('openlofi-'+[guid]::NewGuid().ToString('N')+'.zip')
    $temporary=$true
    Write-Output 'Downloading Open Lo-Fi CC0 source pack (~529 MiB). Only 24 selected tracks will be installed.'
    Invoke-WebRequest 'https://github.com/btahir/open-lofi/releases/download/v1.0.0/openlofi.zip' -OutFile $Archive
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Archive).Path)
try{
    foreach($track in $catalog){
        $entry=$zip.Entries | Where-Object { $_.FullName -eq $track.file } | Select-Object -First 1
        if(-not $entry){throw ('Missing track: '+$track.file)}
        $folder=Join-Path $Destination $track.playlist
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        $target=Join-Path $folder $track.file
        $pending=$target+'.download'
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$pending,$true)
        if((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $track.sha256){Remove-Item -LiteralPath $pending;throw ('Checksum mismatch: '+$track.file)}
        Move-Item -LiteralPath $pending -Destination $target -Force
    }
    Copy-Item (Join-Path $root 'assets\music\catalog.json'),(Join-Path $root 'assets\music\LICENSE-CC0.txt') $Destination -Force
}finally{$zip.Dispose();if($temporary){Remove-Item -LiteralPath $Archive}}
Write-Output ('Installed 12 Ambient + 12 Lo-fi tracks: '+$Destination)
