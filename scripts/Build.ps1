param([switch]$Test,[switch]$TestCore,[string[]]$Suites)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root 'artifacts\app'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc=Join-Path $framework 'csc.exe'
$metadata=Join-Path $env:WINDIR 'System32\WinMetadata'
$core=Join-Path $output 'WinDynamicIsland.Core.dll'
$coreFiles=Get-ChildItem (Join-Path $root 'src\WinDynamicIsland.Core') -Filter '*.cs' | ForEach-Object FullName
& $csc /nologo /target:library /optimize+ /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ('/out:'+$core) @coreFiles
if($LASTEXITCODE){throw 'Core build failed'}
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll','/reference:System.Security.dll','/reference:System.Xml.dll',('/reference:'+$core))
foreach($name in @('WindowsBase','PresentationCore')){$refs+='/reference:'+(Join-Path $framework ('WPF\'+$name+'.dll'))}
foreach($name in @('System.Runtime','System.Runtime.InteropServices.WindowsRuntime')){$refs+='/reference:'+(Join-Path $framework ($name+'.dll'))}
foreach($name in @('Windows.Foundation','Windows.Media','Windows.Devices','Windows.Storage','Windows.Networking','Windows.System')){$refs+='/reference:'+(Join-Path $metadata ($name+'.winmd'))}
$resources=@(('/resource:'+(Join-Path $root 'assets\rewind-fill.svg')+',RewindSvg'),('/resource:'+(Join-Path $root 'assets\fast-forward-fill.svg')+',ForwardSvg'),('/resource:'+(Join-Path $root 'assets\bootstrap-icons-LICENSE')+',MediaIconsLicense'))
$sources=Get-ChildItem (Join-Path $root 'src\WinDynamicIsland.App') -Recurse -Filter '*.cs' | Where-Object {$_.FullName -notmatch '\\(obj|bin)\\'} | ForEach-Object FullName
& $csc /nologo /target:winexe /platform:x64 /optimize+ ('/win32icon:'+(Join-Path $root 'assets\WinDynamicIsland.ico')) ('/out:'+(Join-Path $output 'WinDynamicIsland.exe')) @refs @resources @sources
if($LASTEXITCODE){throw 'App build failed'}
& $csc /nologo /target:winexe /platform:x64 /reference:System.Web.Extensions.dll /reference:System.Core.dll ('/reference:'+$core) ('/out:'+(Join-Path $output 'IslandGuard.exe')) (Join-Path $root 'src\WinDynamicIsland.Guard\Program.cs') (Join-Path $root 'src\WinDynamicIsland.App\Integrations\Devices\BatteryBannerPolicy.cs') (Join-Path $root 'src\WinDynamicIsland.App\Integrations\Notifications\NotificationRoutingPolicy.cs')
if($LASTEXITCODE){throw 'Guard build failed'}
& $csc /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll ('/reference:'+$core) ('/win32manifest:'+(Join-Path $root 'src\WinDynamicIsland.SotaSetup\capture.manifest')) ('/out:'+(Join-Path $output 'SotaProfileSetup.exe')) (Join-Path $root 'src\WinDynamicIsland.SotaSetup\Program.cs') (Join-Path $root 'src\WinDynamicIsland.App\Integrations\Vpn\Sota\ApiProfile.cs')
if($LASTEXITCODE){throw 'Sota setup build failed'}
Copy-Item (Join-Path $root 'assets\codex-icon.png') $output -Force
New-Item -ItemType Directory -Path (Join-Path $output 'browser-extension') -Force | Out-Null
Get-ChildItem (Join-Path $root 'extensions\browser') -File | Where-Object {$_.Name -ne 'connection.js'} | Copy-Item -Destination (Join-Path $output 'browser-extension') -Force
if($Test -or $TestCore -or $Suites){
  $selectedSuites=if($Suites){$Suites}elseif($Test){@('SettingsTests','SettingsUiTests','SettingsMotionTests','TelegramNotificationTests','MediaIntegrationTests','BrowserIntegrationTests','CaptureTests','ExperienceTests')}else{@('SettingsTests','SettingsMotionTests','BrowserIntegrationTests','ExperienceTests')}
  foreach($suite in $selectedSuites){
    $suiteExecutable=Join-Path $output ($suite+'.exe')
    $extraSources=@();if($suite -eq 'CaptureTests'){$extraSources+=(Join-Path $root 'src\WinDynamicIsland.SotaSetup\Program.cs')}
    & $csc /nologo /target:exe /platform:x64 ('/main:WinDynamicIsland.'+$suite) ('/out:'+$suiteExecutable) @refs @resources @sources @extraSources (Join-Path $root ('tests\'+$suite+'.cs'))
    if($LASTEXITCODE){throw ($suite+' build failed')}
    & $suiteExecutable
    if($LASTEXITCODE){throw ($suite+' failed')}
  }
}
Write-Output ('Build ready: '+$output)
