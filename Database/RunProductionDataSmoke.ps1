param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$database=Get-Content -LiteralPath (Join-Path $RunDirectory 'database_name.txt')
if($database -notmatch '^materijalno_fox_[0-9]{8}_[0-9]{6}$'){throw 'New production catalog required.'}
$build=Join-Path $PSScriptRoot '..\artifacts\FoxApp'
$output=Join-Path $build 'ProductionDataSmoke.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll','ReachFramework.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Xml.dll /reference:System.Windows.Forms.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'ProductionDataSmoke.cs')
if($LASTEXITCODE -ne 0){throw 'Smoke compilation failed.'}
$config=[xml](Get-Content -Raw -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config'))
foreach($entry in $config.configuration.connectionStrings.add|Where-Object {$_.name -ne 'OracleFinansije'}){
 $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$entry.connectionString);$builder['Initial Catalog']=$database;$entry.SetAttribute('connectionString',$builder.ConnectionString)
}
$config.Save($output+'.config')
& $output $database $RunDirectory | Tee-Object -FilePath (Join-Path $RunDirectory 'read_only_smoke.log')
if($LASTEXITCODE -ne 0){throw 'Production read-only smoke failed.'}
