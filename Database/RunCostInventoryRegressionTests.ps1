param([Parameter(Mandatory=$true)][string]$WorkDirectory,[Parameter(Mandatory=$true)][string]$BuildDirectory,[Parameter(Mandatory=$true)][string]$ExpectedDatabase)
$ErrorActionPreference='Stop'
$output=Join-Path $BuildDirectory 'CostInventoryRegressionTests.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','ReachFramework.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Xml.dll /reference:System.Windows.Forms.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'CostInventoryRegressionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Audit compilation failed.'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Destination ($output+'.config') -Force
& $output $ExpectedDatabase (Join-Path $WorkDirectory 'wpf_costs') | Tee-Object -FilePath (Join-Path $WorkDirectory 'cost_inventory_regression_tests.log')
if($LASTEXITCODE -ne 0){throw 'Read-only inventory audit failed.'}

