param([ValidateSet('plan','rehearse','apply')][string]$Mode='plan')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_material_identifiers_run.txt')).Trim()
if($Mode -ne 'plan' -and (Get-Process Materijalno.UI -ErrorAction SilentlyContinue)){throw 'Close the WPF application before changing its identifier columns.'}
if($Mode -eq 'apply'){
 $proof=Get-Content -LiteralPath (Join-Path $work 'migration_rehearsal.json') -Raw|ConvertFrom-Json
 if(-not $proof.Passed -or -not $proof.RehearsalRolledBack){throw 'Successful rollback rehearsal required.'}
}
$build=Join-Path $root 'artifacts/IdentifierQA';$exe=Join-Path $build 'MigrateMaterialIdentifiers.exe'
$refs=@('Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.Data.SqlClient.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
$refs+='/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\System.Data.Common.dll'
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'MigrateMaterialIdentifiers.cs')
if($LASTEXITCODE -ne 0){throw 'Migration compilation failed.'}
Copy-Item -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config') -Destination ($exe+'.config')
& $exe $work (Join-Path $root 'Materijalno.UI/App.config') $Mode | Set-Content -LiteralPath (Join-Path $work ($Mode+'.log')) -Encoding utf8
if($LASTEXITCODE -ne 0){Get-Content -LiteralPath (Join-Path $work ($Mode+'.log')) -Tail 14;throw 'Identifier migration failed; transaction not committed.'}
Get-Content -LiteralPath (Join-Path $work ($Mode+'.log')) -Tail 12
