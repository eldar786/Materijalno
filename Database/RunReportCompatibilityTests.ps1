param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$BuildDirectory,[string]$TestConfiguration)
$ErrorActionPreference='Stop'
$build=if($BuildDirectory){$BuildDirectory}else{Join-Path $PSScriptRoot '..\artifacts\PrometQA'}
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$output=Join-Path $build 'ReportCompatibilityTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll') | ForEach-Object {'/reference:'+(Join-Path $build $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','ReachFramework.dll') | ForEach-Object {'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Xml.dll /reference:System.Windows.Forms.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'ReportCompatibilityTests.cs')
if($LASTEXITCODE -ne 0){throw 'Compilation failed.'}
if(-not $TestConfiguration){$TestConfiguration=Join-Path $build 'PrometScenarioTests.exe.config'}
Copy-Item -LiteralPath $TestConfiguration -Destination ($output+'.config')
$config=[xml](Get-Content -LiteralPath ($output+'.config') -Raw)
$b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($config.configuration.connectionStrings.add | Where-Object name -eq MaterijalnoDb).connectionString))
if($b.InitialCatalog -ne 'materijalno_fox_test_20261104_215755'){throw 'Expected isolated test catalog.'}
& $output (Join-Path $WorkDirectory 'report_hotfix') | Tee-Object -FilePath (Join-Path $WorkDirectory 'report_compatibility_tests.log')
if($LASTEXITCODE -ne 0){throw 'Compatibility tests failed.'}
