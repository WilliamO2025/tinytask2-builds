param([ValidateSet('User','InputLab')][string]$Product='User',[string]$Original='C:\Users\Admin\Downloads\tinytask.exe')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$label=if($Product -eq 'InputLab'){'TinyTask 2.0 Input Lab'}else{'TinyTask 2.0'}
$id=if($Product -eq 'InputLab'){'TinyTask2.InputLab.Desktop'}else{'TinyTask2.Classic.Desktop'}
$guid=if($Product -eq 'InputLab'){'{DEB0B44A-BD91-4FD5-823D-0350BE63D191}'}else{'{A7EEA785-D560-48E3-A155-844B5642E13C}'}
$relative=if($Product -eq 'InputLab'){'Programs\TinyTask2-InputLab\TinyTask2-InputLab.exe'}else{'Programs\TinyTask2\TinyTask2.exe'}
$expected=Join-Path $env:LOCALAPPDATA $relative
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR=Join-Path $root '.tools\bundles'
$env:TINYTASK_LAB_DATA=Join-Path $root 'tests\launch-settings'
New-Item -ItemType Directory -Path $env:TINYTASK_LAB_DATA -Force | Out-Null
'{"SetupSeen":true}' | Set-Content (Join-Path $env:TINYTASK_LAB_DATA 'settings.json')
$originalHash=(Get-FileHash -LiteralPath $Original).Hash
$originalProcess=Get-Process -Name tinytask -ErrorAction SilentlyContinue | Where-Object Path -eq $Original | Select-Object -First 1
if(!$originalProcess){$originalProcess=Start-Process -FilePath $Original -WindowStyle Normal -PassThru}
Start-Sleep -Milliseconds 500
$results=@()
$shell=New-Object -ComObject WScript.Shell
$propertyShell=New-Object -ComObject Shell.Application
$entries=@(
    @{Name='exe';Path=$expected},
    @{Name='desktop';Path=(Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($label+'.lnk'))},
    @{Name='start-menu';Path=(Join-Path ([Environment]::GetFolderPath('Programs')) ($label+'.lnk'))}
)
foreach($entry in $entries) {
    $appId=$null
    if($entry.Path.EndsWith('.lnk')) {
        $link=$shell.CreateShortcut($entry.Path)
        if($link.TargetPath -ne $expected -or $link.Arguments) {throw "Wrong shortcut: $($entry.Path)"}
        $folder=$propertyShell.Namespace((Split-Path $entry.Path))
        $appId=$folder.ParseName((Split-Path $entry.Path -Leaf)).ExtendedProperty('System.AppUserModel.ID')
        if($appId -ne $id) {throw "Wrong shortcut App ID: $appId"}
    }
    $env:TINYTASK_LAB_LAUNCH_REPORT=Join-Path $root "tests\launch-$($entry.Name)-$([Guid]::NewGuid().ToString('N')).json"
    $null=Start-Process -FilePath $entry.Path -WindowStyle Normal -PassThru
    for($i=0;$i -lt 100 -and !(Test-Path $env:TINYTASK_LAB_LAUNCH_REPORT);$i++){Start-Sleep -Milliseconds 100}
    if(!(Test-Path $env:TINYTASK_LAB_LAUNCH_REPORT)){throw "No GUI launch report for $($entry.Name)"}
    $report=Get-Content -Raw $env:TINYTASK_LAB_LAUNCH_REPORT | ConvertFrom-Json
    $process=Get-Process -Id $report.pid
    if($process.Path -ne $expected -or !$report.visible -or $report.appId -ne $id){throw 'Wrong launched application'}
    $originalProcess.Refresh()
    $results+= [pscustomobject]@{route=$entry.Name;shortcut=$entry.Path;shortcutAppId=$appId;actual=$report;originalRunning=(!$originalProcess.HasExited);originalWindow=$originalProcess.MainWindowHandle.ToInt64();originalPath=$originalProcess.Path;passed=$true}
    $null=$process.CloseMainWindow()
    if(!$process.WaitForExit(5000)){throw 'Our app did not close normally'}
}
$key=Get-ItemProperty ("HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\"+$guid)
if($key.UninstallString -ne ('"'+$expected+'" --uninstall')){throw 'Incorrect uninstall path'}
$unchanged=(Get-FileHash -LiteralPath $Original).Hash -eq $originalHash
if(!$unchanged){throw 'Original executable changed'}
[pscustomobject]@{time=[DateTimeOffset]::UtcNow;allPassed=$true;originalExecutableUnchanged=$unchanged;uninstall=$key.UninstallString;productGuid=$key.ProductGuid;results=$results} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $root ("tests\launch-coexistence-"+$Product+".json"))
Get-Content (Join-Path $root ("tests\launch-coexistence-"+$Product+".json"))
