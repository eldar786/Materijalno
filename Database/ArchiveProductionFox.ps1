param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,[Parameter(Mandatory=$true)][string]$FoxFolder,[Parameter(Mandatory=$true)][string]$ResultPath)
$ErrorActionPreference='Stop'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$output=Join-Path $PSScriptRoot '..\artifacts\ProductionImport\ArchiveProductionFox.exe'
& $compiler /nologo /target:exe /langversion:latest ('/out:'+$output) /reference:System.Data.dll /reference:System.Core.dll /reference:System.Xml.Linq.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'ArchiveProductionFox.cs')
if($LASTEXITCODE -ne 0){throw 'Archive compilation failed.'}
& $output $DatabaseName $FoxFolder (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config') $ResultPath
if($LASTEXITCODE -ne 0){throw 'Complete production archive verification failed.'}
