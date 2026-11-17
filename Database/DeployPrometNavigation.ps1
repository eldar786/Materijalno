$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_promet_navigation_run.txt')).Trim()
if((Get-Content -LiteralPath (Join-Path $work 'tests.log') -Raw) -notmatch 'ALL PASSED 548;'){throw 'Navigation tests must pass before deployment.'}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before deployment.'}
$source=Join-Path $root 'Materijalno.UI/bin/Debug'
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb')
$results=@()
foreach($relative in @('artifacts/FoxApp','artifacts/PrometTest')){
    $target=Join-Path $root $relative
    if(-not(Test-Path -LiteralPath $target)){continue}
    foreach($file in $files){
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $target $file)
        if((Get-FileHash -LiteralPath (Join-Path $source $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $file)).Hash){throw ('Deployment mismatch: '+$file)}
    }
    $results+=@{Target=$relative;Files=$files.Count;ConfigUnchanged=$true}
}
foreach($before in (Get-Content -LiteralPath (Join-Path $work 'configs_before.json') -Raw|ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath $before.Path).Hash -ne $before.Hash){throw ('Configuration changed: '+$before.Path)}
}
$results|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $work 'deployment.json') -Encoding utf8
$results|ForEach-Object{[pscustomobject]$_}|Format-Table
