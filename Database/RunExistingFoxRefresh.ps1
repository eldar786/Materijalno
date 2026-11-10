param([switch]$Apply)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_fox_refresh_run.txt')).Trim()
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close the new WPF application before refreshing its data.'}
$exe=Join-Path $work 'RefreshExistingFoxData.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /langversion:latest ('/out:'+$exe) /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Core.dll /reference:System.Xml.Linq.dll /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'DbfReader.cs') (Join-Path $PSScriptRoot 'RefreshExistingFoxData.cs')
if($LASTEXITCODE -ne 0){throw 'Refresh tool compilation failed.'}
$arguments=@((Join-Path $work 'source'),(Join-Path $root 'Materijalno.UI/App.config'),$work)
$log='rehearsal.log'
if($Apply){
 $proof=Get-Content -LiteralPath (Join-Path $work 'rehearsal_result.json') -Raw|ConvertFrom-Json
 if(-not $proof.Passed -or -not $proof.RehearsalRolledBack -or -not $proof.SchemaUnchanged){throw 'Verified rollback rehearsal required.'}
 $arguments+='apply-existing-Fox-refresh';$log='refresh.log'
}
& $exe @arguments | Tee-Object -FilePath (Join-Path $work $log)
if($LASTEXITCODE -ne 0){throw 'Existing Fox data refresh failed; its data transaction was rolled back.'}
