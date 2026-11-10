param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')
$database=(Get-Content -LiteralPath (Join-Path $RunDirectory 'database_name.txt')).Trim()
if($database -notmatch '^materijalno_fox_[0-9]{8}_[0-9]{6}$'){throw 'New production catalog required.'}
foreach($name in @('all_tables_verification.json','operational_verification.json','native_and_source_verification.json')){
 $result=Get-Content -Raw -LiteralPath (Join-Path $RunDirectory $name)|ConvertFrom-Json
 if(-not $result.Passed -or $result.Database -ne $database){throw ('Verification missing: '+$name)}
}
$backupResult=Get-Content -Raw -LiteralPath (Join-Path $RunDirectory 'backup_result.json')|ConvertFrom-Json
if(-not $backupResult.Verified -or $backupResult.Database -ne $database){throw 'Verified database backup required.'}
if((Get-Content -Raw -LiteralPath (Join-Path $RunDirectory 'read_only_smoke.log')) -notmatch 'ALL PASSED production'){throw 'Read-only application smoke required.'}
$mainExe=Join-Path $workspace 'artifacts\FoxApp\Materijalno.UI.exe'
if(@(Get-Process -Name 'Materijalno.UI' -ErrorAction SilentlyContinue|Where-Object {$_.Path -eq $mainExe}).Count){throw 'Working application must be closed before switching.'}
$paths=@('Materijalno.UI\App.config','Materijalno.Model\App.config','Materijalno.ViewModel\app.config')|ForEach-Object{Join-Path $workspace $_}
$paths+=@(Get-ChildItem -LiteralPath (Join-Path $workspace 'artifacts\FoxApp') -File -Filter '*.config'|Where-Object {$_.Name -notmatch '(Tests|Smoke)\.exe\.config$'}|ForEach-Object FullName)
$backupFolder=Join-Path $RunDirectory 'configuration_before';$null=New-Item -ItemType Directory -Path $backupFolder -Force
$saved=@();$changed=@()
try{
 foreach($file in $paths){
  $file=[IO.Path]::GetFullPath($file)
  if(-not $file.StartsWith($workspace+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Configuration outside workspace.'}
  $relative=$file.Substring($workspace.Length+1);$backup=Join-Path $backupFolder ($relative.Replace('\','__'))
  if(Test-Path -LiteralPath $backup){throw 'Configuration backup already exists; do not overwrite rollback evidence.'}
  Copy-Item -LiteralPath $file -Destination $backup
  $saved+=@{File=$file;Backup=$backup}
  $xml=[xml](Get-Content -Raw -LiteralPath $file)
  foreach($entry in $xml.SelectNodes('/configuration/connectionStrings/add')){
   if($entry.GetAttribute('name') -eq 'OracleFinansije'){continue}
   $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new($entry.GetAttribute('connectionString'))
   if($builder.InitialCatalog -notmatch '^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$'){throw ('Unexpected database in '+$relative)}
   $builder['Initial Catalog']=$database;$entry.SetAttribute('connectionString',$builder.ConnectionString)
  }
  $settings=$xml.SelectSingleNode('/configuration/appSettings')
  if(-not $settings){$settings=$xml.CreateElement('appSettings');$null=$xml.DocumentElement.AppendChild($settings)}
  $flag=$xml.SelectSingleNode('/configuration/appSettings/add[@key="OracleExportEnabled"]')
  if(-not $flag){$flag=$xml.CreateElement('add');$flag.SetAttribute('key','OracleExportEnabled');$null=$settings.AppendChild($flag)}
  $flag.SetAttribute('value','false')
  $xml.Save($file);$changed+=$relative
 }
 $result=@{Database=$database;Switched=$true;Configurations=$changed;RollbackFolder=$backupFolder;OracleExportEnabled=$false;TestApplicationUnchanged=$true}
 $result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $RunDirectory 'application_switch.json') -Encoding utf8
 'Application now uses verified production data: '+$database
}catch{foreach($copy in $saved){Copy-Item -LiteralPath $copy.Backup -Destination $copy.File -Force};throw}
