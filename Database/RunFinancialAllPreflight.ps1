$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_financial_all_run.txt')).Trim()
$build=Join-Path $root 'artifacts/FoxApp';$exe=Join-Path $build 'InspectFinancialAll.exe'
$refs=@('Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Oracle.ManagedDataAccess.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'InspectFinancialAll.cs')
if($LASTEXITCODE -ne 0){throw 'Preflight compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config') -Raw)
$entry=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
$null=[Reflection.Assembly]::LoadFrom((Join-Path $build 'Oracle.ManagedDataAccess.dll'))
$b=[Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder]::new([string]$entry.connectionString)
if($b.DataSource -ne '192.168.1.224:1521/TESTPDB.LUTRIJABIH.BA' -or $b.UserID -ne 'IBS'){throw 'Only TESTPDB/IBS allowed.'}
$cfg.Save($exe+'.config')
& $exe $work | Tee-Object -FilePath (Join-Path $work 'preflight.log')
if($LASTEXITCODE -ne 0){throw 'Financial preflight failed; no journals transferred.'}
