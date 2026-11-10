param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_test_[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,[ValidateSet('save','reports')][string]$Mode='save',[Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$BuildDirectory=(Join-Path $PSScriptRoot '..\artifacts\FoxApp'))
$ErrorActionPreference='Stop'
$output=Join-Path $BuildDirectory 'PrometScenarioTests.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll') | ForEach-Object {'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','ReachFramework.dll','System.Printing.dll') | ForEach-Object {'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
$inputs=@((Join-Path $PSScriptRoot 'PrometScenarioTests.cs'),(Join-Path $PSScriptRoot 'ScenarioReportTests.cs'))
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) '/reference:System.Data.dll' '/reference:System.Configuration.dll' '/reference:System.Core.dll' '/reference:System.Web.Extensions.dll' '/reference:System.Xml.dll' '/reference:System.Xml.Linq.dll' '/reference:System.Windows.Forms.dll' '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs $inputs
if($LASTEXITCODE -ne 0){throw 'Scenario test compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
foreach($node in $config.configuration.connectionStrings.add | Where-Object {$_.name -ne 'OracleFinansije'}) {
 $b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$node.connectionString);$b['Initial Catalog']=$DatabaseName;$node.SetAttribute('connectionString',$b.ConnectionString)
}
$config.Save($output+'.config')
& $output $Mode $WorkDirectory | Tee-Object -FilePath (Join-Path $WorkDirectory ($Mode+'_tests.log'))
if($LASTEXITCODE -ne 0){throw 'Scenario tests failed.'}
