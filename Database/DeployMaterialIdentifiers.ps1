$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_material_identifiers_run.txt')).Trim()
$proof=Get-Content -LiteralPath (Join-Path $work 'migration_result.json') -Raw | ConvertFrom-Json
if(-not $proof.Passed -or -not $proof.Applied){throw 'Committed identifier migration required.'}
if((Get-Content -LiteralPath (Join-Path $work 'presentation_tests.log') -Raw) -notmatch 'ALL PASSED 56;'){throw 'Presentation checks required.'}
if(Get-Process Materijalno.UI -ErrorAction SilentlyContinue){throw 'Close Materijalno before deploying.'}
$source=Join-Path $root 'Materijalno.UI/bin/Debug'
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb')
$files+=Get-ChildItem -LiteralPath (Join-Path $source 'Izvjestaji') -Filter '*.rdlc' | ForEach-Object {'Izvjestaji/'+$_.Name}
$results=@()
foreach($relative in @('artifacts/FoxApp','artifacts/PrometTest')){
    $target=Join-Path $root $relative
    if(-not (Test-Path -LiteralPath $target)){continue}
    foreach($file in $files){
        $dest=Join-Path $target $file
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $source $file) -Destination $dest
        if((Get-FileHash -LiteralPath $dest).Hash -ne (Get-FileHash -LiteralPath (Join-Path $source $file)).Hash){throw ('Deployment mismatch: '+$file)}
    }
    $results+=@{Target=$relative;Files=$files.Count;ConfigUnchanged=$true}
}
foreach($before in (Get-Content -LiteralPath (Join-Path $work 'config_hashes_before.json') -Raw | ConvertFrom-Json)){
    if((Get-FileHash -LiteralPath $before.Path).Hash -ne $before.Hash){throw ('Config changed unexpectedly: '+$before.Path)}
}
$results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $work 'deployment.json') -Encoding utf8
$results | ForEach-Object {[pscustomobject]$_} | Format-Table
