param([switch]$Publish)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_CLI_HOME=Join-Path $projectRoot '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:NUGET_PACKAGES=Join-Path $projectRoot '.tools\packages'
$dotnet=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path $dotnet)) { $dotnet='dotnet' }
$project=Join-Path $projectRoot 'src\TinyTask.Diagnostics'
& $dotnet build $project -c Release
if($LASTEXITCODE -ne 0) {throw 'Build failed'}
if($Publish) {
    & $dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o (Join-Path $projectRoot 'dist\win-x64')
    if($LASTEXITCODE -ne 0) {throw 'Publish failed'}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\win-x64\TinyTask2.exe') -Destination (Join-Path $projectRoot 'dist\TinyTask2.Setup.exe')
    & $dotnet publish $project -c Release -r win-x64 --self-contained true -p:InputLab=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o (Join-Path $projectRoot 'dist\inputlab-win-x64')
    if($LASTEXITCODE -ne 0) {throw 'Input Lab publish failed'}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'dist\inputlab-win-x64\TinyTask2-InputLab.exe') -Destination (Join-Path $projectRoot 'dist\TinyTask2-InputLab.Setup.exe')
}
