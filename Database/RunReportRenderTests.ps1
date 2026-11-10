param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$build=Join-Path $PSScriptRoot '..\artifacts\FoxApp'
$output=Join-Path $build 'FoxReportTests.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll') | ForEach-Object {'/reference:'+(Join-Path $build $_)}
& $compiler /nologo /target:exe /langversion:latest (('/out:')+$output) '/reference:System.Data.dll' '/reference:System.Xml.dll' '/reference:System.Xml.Linq.dll' '/reference:System.Core.dll' '/reference:System.Windows.Forms.dll' $refs (Join-Path $PSScriptRoot 'ReportRenderTests.cs')
if($LASTEXITCODE -ne 0){throw 'Report test compilation failed.'}
Copy-Item -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config') -Destination ($output+'.config')
& $output (Join-Path $PSScriptRoot '..\Materijalno.UI\Izvjestaji') $OutputDirectory | Tee-Object -FilePath (Join-Path $OutputDirectory '..\report_tests.log')
if($LASTEXITCODE -ne 0){throw 'Report render tests failed.'}
