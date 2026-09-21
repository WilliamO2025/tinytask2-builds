param([switch]$Test)
$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'MSVC x64 build tools are required for this maintainer build'}
$vcvars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
$out=Join-Path $root 'dist/route-probe'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Push-Location $PSScriptRoot
try {
    & cmd.exe /d /c "call `"$vcvars`" >nul && cl.exe /nologo /EHsc /W4 /WX /std:c++17 /DUNICODE /MT route-native.cpp user32.lib gdi32.lib shell32.lib /Fe:`"$out/TinyTask2-NativeRouteProbe.exe`" /link /SUBSYSTEM:WINDOWS"
    if($LASTEXITCODE -ne 0){throw 'Native probe build failed'}
    if($Test){
        $report=Join-Path $out ('synthetic-'+[Guid]::NewGuid().ToString('N')+'.json')
        # Visible windows are required for this interactive foreground/pointer test.
        $process=Start-Process -FilePath (Join-Path $out 'TinyTask2-NativeRouteProbe.exe') -ArgumentList @('--report',('"'+$report+'"'),'--self-test') -PassThru -WindowStyle Normal
        if(!$process.WaitForExit(30000)){Stop-Process -Id $process.Id;throw 'Native probe exceeded its test timeout'}
        if($process.ExitCode -ne 0){throw ('Native probe exited with code '+$process.ExitCode)}
        if(!(Test-Path -LiteralPath $report)){throw 'Native probe did not produce a report'}
        $result=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        $result | ConvertTo-Json -Depth 5
        if(!$result.syntheticPassed -or $result.unhookApiFailures -ne 0){throw 'Routing control failed'}
    }
} finally {Pop-Location}
