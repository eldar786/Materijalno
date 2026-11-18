param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\InitialStockEditDeleteQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_initial_stock_edit_delete_run.txt')).Trim()
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before real edit/delete tests.'}
$exe=Join-Path $BuildDirectory 'InitialStockEditDeleteChecks.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'InitialStockEditDeleteChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Edit/delete check compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije';if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
& $exe $work | Tee-Object -FilePath (Join-Path $work 'checks.log')
if($LASTEXITCODE -ne 0){throw 'Edit/delete tests failed; inspect result/cleanup before retrying.'}
