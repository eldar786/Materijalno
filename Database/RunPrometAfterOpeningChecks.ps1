param([string]$WorkDirectory,[switch]$EdgeOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WorkDirectory){$WorkDirectory=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_promet_after_opening_run.txt') -Raw).Trim()}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before rollback tests.'}
$build=Join-Path $WorkDirectory 'build';$exe=Join-Path $build 'PrometAfterOpeningChecks.exe'
$refs=@('Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.EntityFrameworkCore.SqlServer.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'PrometAfterOpeningChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Promet rollback checks compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
$log=Join-Path $WorkDirectory 'checks.log'
if(Test-Path -LiteralPath $log){Copy-Item -LiteralPath $log -Destination (Join-Path $WorkDirectory 'checks_previous.log') -Force}
$arguments=@($WorkDirectory)
if($EdgeOnly){$arguments+='edge-only'}
& $exe @arguments *> $log
$code=$LASTEXITCODE
Get-Content -LiteralPath $log -Tail 30
if($code -ne 0){throw 'Promet comparison failed. All test transactions were rolled back.'}
Get-Content -LiteralPath (Join-Path $WorkDirectory 'result.json') -Raw
