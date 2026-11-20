param([string]$WorkDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WorkDirectory){$WorkDirectory=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_inventory_entry_fix_run.txt') -Raw).Trim()}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close the user application before temporary inventory tests.'}
$build=Join-Path $WorkDirectory 'build';$exe=Join-Path $build 'InventoryEntryWorkflowChecks.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll')|ForEach-Object{'/reference:'+(Join-Path $build $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'InventoryEntryWorkflowChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Workflow checks compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
$archive=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_mat_inv_archive_import.txt') -Raw).Trim()
$log=Join-Path $WorkDirectory 'workflow_checks.log'
if(Test-Path -LiteralPath $log){Copy-Item -LiteralPath $log -Destination (Join-Path $WorkDirectory 'workflow_checks_previous.log') -Force}
& $exe $WorkDirectory (Join-Path $archive 'source') *> $log
$code=$LASTEXITCODE
Get-Content -LiteralPath $log -Tail 35
if($code -ne 0){throw 'Inventory workflow tests failed; inspect results and cleanup before retrying.'}
Get-Content -LiteralPath (Join-Path $WorkDirectory 'workflow_result.json') -Raw
