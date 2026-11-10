param([ValidateSet('check','send-M-191-to-TESTPDB')][string]$Mode='check')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_oracle_m191_run.txt')).Trim()
$build=Join-Path $root 'artifacts/FoxApp'
$output=Join-Path $build 'VerifyM191OracleTransfer.exe'
$refs=@('Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Oracle.ManagedDataAccess.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Xml.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'VerifyM191OracleTransfer.cs')
if($LASTEXITCODE -ne 0){throw 'M191 test compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $build 'Materijalno.UI.exe.config') -Raw)
$connection=$config.configuration.connectionStrings.add|Where-Object name -eq OracleFinansije
$builder=[Data.Common.DbConnectionStringBuilder]::new();$builder.set_ConnectionString([string]$connection.connectionString)
if([string]$builder['Data Source'] -ne '192.168.1.224:1521/TESTPDB.LUTRIJABIH.BA' -or [string]$builder['User Id'] -ne 'IBS'){throw 'Only the confirmed IBS TESTPDB target is permitted.'}
($config.configuration.appSettings.add|Where-Object key -eq OracleExportEnabled).SetAttribute('value','true')
# Only this purpose-built verifier is enabled here. The application config is untouched.
$config.Save($output+'.config')
& $output $Mode $work | Tee-Object -FilePath (Join-Path $work ($Mode+'.log'))
if($LASTEXITCODE -ne 0){throw 'M191 test failed.'}
