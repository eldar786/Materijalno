param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_test_[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,[string]$LogPath,[string]$BuildDirectory=(Join-Path $PSScriptRoot '..\artifacts\FoxApp'))
$ErrorActionPreference='Stop'
$build=$BuildDirectory
$output=Join-Path $build 'FoxIntegrationTests.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.Abstractions.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll') | ForEach-Object {'/reference:'+(Join-Path $build $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll') | ForEach-Object {'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /langversion:latest (('/out:')+$output) '/reference:System.Configuration.dll' '/reference:System.Data.dll' '/reference:System.Core.dll' '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'IntegrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Integration test compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config') -Raw)
$node=$config.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb'
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$node.connectionString)
$builder['Initial Catalog']=$DatabaseName
$node.SetAttribute('connectionString',$builder.ConnectionString)
$config.Save($output+'.config')
if($LogPath){& $output | Tee-Object -FilePath $LogPath}else{& $output}
if($LASTEXITCODE -ne 0){throw 'Integration tests failed.'}
