param([string]$WorkDirectory,[switch]$Apply)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WorkDirectory){$WorkDirectory=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_year_close_physical_run.txt') -Raw).Trim()}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before the authorized 2025 comparison.'}
$build=Join-Path $WorkDirectory 'build';$exe=Join-Path $build 'YearCloseFoxRollbackChecks.exe'
$refs=@('Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.Data.SqlClient.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'YearCloseFoxRollbackChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Physical comparison compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
$arguments=@($WorkDirectory,'C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\2025',(Join-Path $WorkDirectory 'fox_working_copy'))
$log=Join-Path $WorkDirectory 'physical_checks.log'
if($Apply){$arguments+='apply-physical-Fox-year-close';$log=Join-Path $WorkDirectory 'physical_apply.log'}
& $exe @arguments *> $log
$code=$LASTEXITCODE
Get-Content -LiteralPath $log -Tail 25
if($code -ne 0){throw 'Physical Fox comparison failed; its transaction was not committed.'}
