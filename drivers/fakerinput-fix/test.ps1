param([switch]$Baseline)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$source=if($Baseline){Join-Path $root 'upstream/Queue.c'}else{Join-Path $root 'Queue.c'}
$text=[IO.File]::ReadAllText($source).Replace("`r`n","`n")
if($Baseline){
    # C++ test compilation needs case scopes; these braces do not change the C logic.
    $text=$text.Replace("case REPORTID_CONTROL:`n","case REPORTID_CONTROL: {`n").Replace("    case REPORTID_CHECK_API_VERSION:`n","    }`n    case REPORTID_CHECK_API_VERSION:`n").Replace("case REPORTID_API_VERSION_FEATURE_ID:`n","case REPORTID_API_VERSION_FEATURE_ID: {`n")
    $feature=$text.IndexOf("NTSTATUS`nGetFeatureReport(")
    $text=$text.Substring(0,$feature)+$text.Substring($feature).Replace("        break;`n    default:","        break;`n    }`n    default:")
}
$define=if($Baseline){'/DTT_BASELINE'}else{''}
$start=$text.IndexOf("NTSTATUS`nWriteReport(")
if($start -lt 0){throw 'Expected upstream WriteReport not found'}
$end=$text.IndexOf("NTSTATUS`nGetStringId(",$start)
if($start -lt 0 -or $end -lt 0){throw 'Expected upstream routines not found'}
[IO.File]::WriteAllText((Join-Path $root 'routines.inc'),$text.Substring($start,$end-$start))
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation=& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$installation){throw 'MSVC x64 build tools are required for this maintainer test'}
$vcvars=Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
Push-Location $root
try {
    & cmd.exe /d /c "call `"$vcvars`" >nul && cl.exe $define /nologo /EHsc /W4 /WX /std:c++17 /Od /Zi test.cpp /Fe:report-tests.exe"
    if($LASTEXITCODE -ne 0){throw 'Report test build failed'}
    & (Join-Path $root 'report-tests.exe')
    if($LASTEXITCODE -ne 0){throw 'Report regression failed'}
} finally {Pop-Location}
