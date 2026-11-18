param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\InitialStockQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_initial_stock_posting_run.txt')).Trim()
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before real temporary postings.'}
$exe=Join-Path $BuildDirectory 'InitialStockPostingChecks.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'InitialStockPostingChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Posting test compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije';if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
& $exe $work | Tee-Object -FilePath (Join-Path $work 'posting_checks.log')
if($LASTEXITCODE -ne 0){throw 'Real posting checks failed; inspect result/cleanup files before retrying.'}
