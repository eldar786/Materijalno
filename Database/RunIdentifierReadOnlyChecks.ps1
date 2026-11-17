param([string]$BuildDirectory='D:\Materijalno Novo\Materijalno.UI\bin\Debug')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_material_identifiers_run.txt')).Trim()
$exe=Join-Path $BuildDirectory 'IdentifierReadOnlyChecks.exe'
$refs=@('Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'IdentifierReadOnlyChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Read-only checks compilation failed.'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Destination ($exe+'.config')
& $exe $work | Tee-Object -FilePath (Join-Path $work 'read_only_checks.log')
if($LASTEXITCODE -ne 0){throw 'Read-only checks failed.'}
