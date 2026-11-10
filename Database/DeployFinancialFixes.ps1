param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$BuildDirectory='D:\Materijalno Novo\Materijalno.UI\bin\Debug')
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$checks=@(@('financial_tests.log','ALL PASSED 92'),@('financial_boundary_tests.log','BOUNDARY PASSED 8'),@('financial_views_tests.log','VIEWS PASSED 13'))
foreach($check in $checks){if((Get-Content -LiteralPath (Join-Path $WorkDirectory $check[0]) -Raw) -notmatch $check[1]){throw ('Required checks missing: '+$check[0])}}
$comparison=Get-Content -LiteralPath (Join-Path $WorkDirectory 'financial_comparison.json') -Raw | ConvertFrom-Json
if($comparison.Failures -ne 0 -or $comparison.LinesChecked -ne 42 -or $comparison.OracleConnectionsUsed){throw 'Fox source comparison must pass without Oracle connections.'}
$targets=@(@{Name='Installed';Path=(Join-Path $workspace 'artifacts\FoxApp');Database='materijalno_fox_20261105_102038'},@{Name='Debug';Path=(Join-Path $workspace 'Materijalno.UI\bin\Debug');Database='materijalno_fox_20261105_102038'},@{Name='Test';Path=(Join-Path $workspace 'artifacts\PrometTest');Database='materijalno_fox_test_20261104_215755'})
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb','Izvjestaji\NaloziZaFinansijePrintanjeNaloga.rdlc')
$backup=Join-Path $WorkDirectory 'installed_before';$configs=@{}
foreach($target in $targets){
 $exe=Join-Path $target.Path 'Materijalno.UI.exe'
 if(@(Get-Process -Name 'Materijalno.UI' -ErrorAction SilentlyContinue|Where-Object {$_.Path -eq $exe}).Count){throw ('Close '+$exe+' before replacement.')}
 $config=$exe+'.config';$configs[$target.Name]=(Get-FileHash -LiteralPath $config).Hash
 $xml=[xml](Get-Content -LiteralPath $config -Raw)
 $catalog=([Data.SqlClient.SqlConnectionStringBuilder]::new([string](($xml.configuration.connectionStrings.add|Where-Object name -eq MaterijalnoDb).connectionString))).InitialCatalog
 if($catalog -ne $target.Database){throw 'Unexpected target database.'}
 if(($xml.configuration.appSettings.add|Where-Object key -eq OracleExportEnabled).value -ne 'false'){throw 'Export must remain disabled for this validation.'}
 foreach($file in $files){$src=Join-Path $BuildDirectory $file;$dst=Join-Path $target.Path $file;$saved=Join-Path (Join-Path $backup $target.Name) $file;if(-not(Test-Path -LiteralPath $src)){throw ('Missing '+$file)};$null=New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force;Copy-Item -LiteralPath $dst -Destination $saved -Force}
 Copy-Item -LiteralPath $config -Destination (Join-Path (Join-Path $backup $target.Name) 'Materijalno.UI.exe.config') -Force
}
$results=@()
try {
 foreach($target in $targets){foreach($file in $files){$src=Join-Path $BuildDirectory $file;$dst=Join-Path $target.Path $file;if([IO.Path]::GetFullPath($src) -ne [IO.Path]::GetFullPath($dst)){Copy-Item -LiteralPath $src -Destination $dst -Force};if((Get-FileHash -LiteralPath $src).Hash -ne (Get-FileHash -LiteralPath $dst).Hash){throw 'Hash mismatch'}}
  if((Get-FileHash -LiteralPath ((Join-Path $target.Path 'Materijalno.UI.exe')+'.config')).Hash -ne $configs[$target.Name]){throw 'Configuration changed.'}
  $results+=@{Target=$target.Name;Path=$target.Path;Files=$files.Count;ConfigurationPreserved=$true;OracleExportEnabled=$false}
 }
}catch{foreach($target in $targets){foreach($file in $files){Copy-Item -LiteralPath (Join-Path (Join-Path $backup $target.Name) $file) -Destination (Join-Path $target.Path $file) -Force}};throw}
@{Targets=$results;Backup=$backup;Completed=Get-Date -Format o;Source=$BuildDirectory}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $WorkDirectory 'financial_deployment.json') -Encoding utf8
'PASS financial fixes installed; working and test database configurations preserved; Oracle export remains disabled.'
