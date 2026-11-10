param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$BuildDirectory='D:\Materijalno Novo\artifacts\FinancialQA',[switch]$BoundaryOnly,[switch]$ViewsOnly)
$ErrorActionPreference='Stop'
$database=(Get-Content -LiteralPath (Join-Path $WorkDirectory 'database_name.txt')).Trim()
if($database -notmatch '^materijalno_fox_test_20261106_[0-9]{6}$'){throw 'Dedicated financial test copy required.'}
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$output=Join-Path $BuildDirectory 'FinancialParityTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','ReachFramework.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Xml.dll /reference:System.Configuration.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'FinancialParityTests.cs')
if($LASTEXITCODE -ne 0){throw 'Financial parity test compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
foreach($entry in @($config.configuration.connectionStrings.add)){
 if($entry.name -eq 'OracleFinansije'){[void]$entry.ParentNode.RemoveChild($entry);continue}
 $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$entry.connectionString);$builder['Initial Catalog']=$database;$entry.SetAttribute('connectionString',$builder.ConnectionString)
}
$flag=$config.configuration.appSettings.add|Where-Object key -eq OracleExportEnabled
if(-not $flag){throw 'Explicit export switch required.'};$flag.SetAttribute('value','false')
$config.Save($output+'.config')
# Only the dedicated QA app may be redirected. Debug/FoxApp retain their configs.
$qa=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\artifacts\FinancialQA')).TrimEnd('\')
if([IO.Path]::GetFullPath($BuildDirectory).TrimEnd('\') -eq $qa){$config.Save((Join-Path $BuildDirectory 'Materijalno.UI.exe.config'))}
$reports=Join-Path $WorkDirectory 'reports';$null=New-Item -ItemType Directory -Path $reports -Force
$arguments=@($database,$reports)
if($BoundaryOnly){$arguments+='boundary'}
if($ViewsOnly){$arguments+='views'}
$log=if($BoundaryOnly){'financial_boundary_tests.log'}elseif($ViewsOnly){'financial_views_tests.log'}else{'financial_tests.log'}
& $output @arguments | Tee-Object -FilePath (Join-Path $WorkDirectory $log)
if($LASTEXITCODE -ne 0){throw 'Financial parity tests failed.'}
