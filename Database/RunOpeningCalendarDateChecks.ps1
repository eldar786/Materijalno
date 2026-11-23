param([Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close the new application before rollback checks.'}
$build=Join-Path $WorkDirectory 'build';$exe=Join-Path $build 'OpeningCalendarDateChecks.exe'
$refs=@('Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'OpeningCalendarDateChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Date check compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
& $exe $WorkDirectory *> (Join-Path $WorkDirectory 'checks.log')
$code=$LASTEXITCODE
Get-Content -LiteralPath (Join-Path $WorkDirectory 'checks.log') -Tail 12
if($code -ne 0){throw 'Opening date checks failed; SQL test writes were rolled back.'}
Get-Content -LiteralPath (Join-Path $WorkDirectory 'result.json') -Raw
