param([string]$WorkDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$WorkDirectory){$WorkDirectory=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_inventory_entry_fix_run.txt') -Raw).Trim()}
$proof=Get-Content -LiteralPath (Join-Path $WorkDirectory 'workflow_result.json') -Raw|ConvertFrom-Json
if(-not $proof.Passed -or -not $proof.Cleaned -or -not $proof.OriginalInventoryRestored){throw 'Passing workflow checks and unchanged SQL data required.'}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before deployment.'}
$build=Join-Path $WorkDirectory 'build'
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb')
$targets=@(
 [pscustomobject]@{Name='Debug';Path=(Join-Path $root 'Materijalno.UI/bin/Debug')},
 [pscustomobject]@{Name='FoxApp';Path=(Join-Path $root 'artifacts/FoxApp')}
)
$hashes=@{}
foreach($target in $targets){
 $config=Join-Path $target.Path 'Materijalno.UI.exe.config'
 $cfg=[xml](Get-Content -LiteralPath $config -Raw)
 $entry=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'MaterijalnoDb'
 $b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$entry.connectionString)
 if($b.DataSource -ne '192.168.1.213' -or $b.InitialCatalog -ne 'materijalno_fox_20261105_102038'){throw ('Unexpected working catalog: '+$target.Name)}
 $hashes[$target.Name]=(Get-FileHash -LiteralPath $config).Hash
 $backup=Join-Path $WorkDirectory ('installed_before/'+$target.Name)
 [void](New-Item -ItemType Directory -Path $backup -Force)
 foreach($file in $files){
  if(-not(Test-Path -LiteralPath (Join-Path $build $file))){throw ('Missing tested binary '+$file)}
  $saved=Join-Path $backup $file
  if(-not(Test-Path -LiteralPath $saved)){Copy-Item -LiteralPath (Join-Path $target.Path $file) -Destination $saved}
 }
}
try{
 foreach($target in $targets){
  foreach($file in $files){
   $src=Join-Path $build $file;$dst=Join-Path $target.Path $file
   Copy-Item -LiteralPath $src -Destination $dst -Force
   if((Get-FileHash -LiteralPath $src).Hash -ne (Get-FileHash -LiteralPath $dst).Hash){throw ('Binary hash mismatch '+$target.Name+'/'+$file)}
  }
  if((Get-FileHash -LiteralPath (Join-Path $target.Path 'Materijalno.UI.exe.config')).Hash -ne $hashes[$target.Name]){throw 'Application configuration changed.'}
 }
}catch{
 foreach($target in $targets){foreach($file in $files){Copy-Item -LiteralPath (Join-Path $WorkDirectory ('installed_before/'+$target.Name+'/'+$file)) -Destination (Join-Path $target.Path $file) -Force}}
 throw
}
$result=[pscustomobject]@{Passed=$true;Checks=$proof.Checks;Targets=$targets;Files=$files;ConfigurationHashesPreserved=$hashes;OriginalSqlDataUnchanged=$proof.OriginalInventoryRestored;UserDeleted1000Ident13NotRestored=$proof.UserDeleted1000Ident13NotRestored}
[IO.File]::WriteAllText((Join-Path $WorkDirectory 'deployment.json'),(ConvertTo-Json -InputObject $result -Depth 6),[Text.UTF8Encoding]::new($false))
$targets|Select-Object Name,Path
'PASS tested binaries installed; configurations unchanged.'
