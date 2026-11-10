$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$designRun=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_design_run.txt')).Trim()
$source=Join-Path $root 'Materijalno.UI/bin/Debug'
$checks=Get-Content -LiteralPath (Join-Path $designRun 'source_checks.json') -Raw|ConvertFrom-Json
if($checks.failures.Count -ne 0){throw 'Menu/binding verification has failures.'}
if((Get-Content -LiteralPath (Join-Path $designRun 'visual_tests.log') -Raw) -notmatch 'ALL PASSED 70;'){throw 'Final visual checks required.'}
$targets=@('artifacts/FoxApp','artifacts/PrometTest','artifacts/DesignQA')
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb')
$result=@()
foreach($relative in $targets){
 $target=Join-Path $root $relative
 if(-not(Test-Path -LiteralPath $target)){continue}
 if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue|Where-Object{$_.Path -eq (Join-Path $target 'Materijalno.UI.exe')}){throw ('Close the app before replacement: '+$relative)}
 $config=Join-Path $target 'Materijalno.UI.exe.config'
 $configHash=(Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash
 $backup=Join-Path $designRun ('installed_before/'+($relative.Replace('/','_')))
 $null=New-Item -ItemType Directory -Path $backup -Force
 Copy-Item -LiteralPath $config -Destination $backup
 foreach($file in $files){
  $dest=Join-Path $target $file
  if(Test-Path -LiteralPath $dest){Copy-Item -LiteralPath $dest -Destination $backup}
  Copy-Item -LiteralPath (Join-Path $source $file) -Destination $dest
  if((Get-FileHash -LiteralPath $dest).Hash -ne (Get-FileHash -LiteralPath (Join-Path $source $file)).Hash){throw ('Installed file mismatch: '+$dest)}
 }
 if((Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash -ne $configHash){throw 'Database configuration changed unexpectedly.'}
 $result+=[pscustomobject]@{Target=$relative;Files=$files.Count;ConfigUnchanged=$true}
}
$result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $designRun 'deployment.json') -Encoding utf8
$result|Format-Table
