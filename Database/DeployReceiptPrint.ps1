$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_receipt_print_run.txt')).Trim()
$tests=Get-Content -LiteralPath (Join-Path $work 'receipt_tests.log') -Raw
if($tests -notmatch 'ALL PASSED 397;' -or $tests -notmatch 'no data written'){throw 'Receipt parity checks must pass first.'}
$source=Join-Path $root 'Materijalno.UI/bin/Debug'
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb','Izvjestaji/UlazMaterijala.rdlc')
$results=@()
foreach($relative in @('artifacts/FoxApp','artifacts/PrometTest')){
 $target=Join-Path $root $relative
 if(-not(Test-Path -LiteralPath $target)){continue}
 if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue|Where-Object{$_.Path -eq (Join-Path $target 'Materijalno.UI.exe')}){throw ('App is open: '+$relative)}
 $backup=Join-Path $work ('installed_before/'+$relative.Replace('/','_'));New-Item -ItemType Directory -Path $backup -Force|Out-Null
 $config=Join-Path $target 'Materijalno.UI.exe.config';$hash=(Get-FileHash -LiteralPath $config).Hash
 Copy-Item -LiteralPath $config -Destination $backup
 foreach($file in $files){
  $dest=Join-Path $target $file;$old=Join-Path $backup $file
  New-Item -ItemType Directory -Path (Split-Path $old -Parent) -Force|Out-Null
  if(Test-Path -LiteralPath $dest){Copy-Item -LiteralPath $dest -Destination $old}
  Copy-Item -LiteralPath (Join-Path $source $file) -Destination $dest
  if((Get-FileHash -LiteralPath $dest).Hash -ne (Get-FileHash -LiteralPath (Join-Path $source $file)).Hash){throw ('Deployment mismatch '+$file)}
 }
 if((Get-FileHash -LiteralPath $config).Hash -ne $hash){throw 'Config changed unexpectedly.'}
 $results+=[pscustomobject]@{Target=$relative;Files=$files.Count;ConfigUnchanged=$true}
}
foreach($before in (Get-Content -LiteralPath (Join-Path $work 'configs_before.json') -Raw|ConvertFrom-Json)){
 if((Get-FileHash -LiteralPath $before.Path).Hash -ne $before.Hash){throw ('Config changed: '+$before.Path)}
}
$results|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'deployment.json') -Encoding utf8
$results|Format-Table
