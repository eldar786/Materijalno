param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$BuildDirectory='D:\Materijalno Novo\artifacts\CostsInventoryQA',[switch]$CostsOnly)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$comparisons=@('cost_report_comparison.json')
if(-not $CostsOnly){$comparisons+='inventory_balance_comparison.json'}
foreach($file in $comparisons){
 $result=Get-Content -LiteralPath (Join-Path $WorkDirectory $file) -Raw | ConvertFrom-Json
 if($result.Failures -ne 0){throw ('Comparison failed: '+$file)}
}
if((Get-Content -LiteralPath (Join-Path $WorkDirectory 'cost_inventory_regression_tests.log') -Raw) -notmatch 'ALL PASSED 25'){throw 'Read-only regressions must pass.'}
if(-not $CostsOnly -and (Get-Content -LiteralPath (Join-Path $WorkDirectory 'report_compatibility_tests.log') -Raw) -notmatch 'ALL PASSED 22'){throw 'Saved inventory compatibility must pass.'}
$targets=@(
 @{Name='Installed';Path=(Join-Path $workspace 'artifacts\FoxApp');Test=$false},
 @{Name='Debug';Path=(Join-Path $workspace 'Materijalno.UI\bin\Debug');Test=$false},
 @{Name='Test';Path=(Join-Path $workspace 'artifacts\PrometTest');Test=$true}
)
$files=@('Materijalno.UI.exe','Materijalno.UI.pdb','Materijalno.Model.dll','Materijalno.Model.pdb','Materijalno.ViewModel.dll','Materijalno.ViewModel.pdb',
 'Izvjestaji\PregledTroskovaPoKontima.rdlc','Izvjestaji\PregledTroskovaPoKontimaINalozimaZaSvaSkladista.rdlc','Izvjestaji\StampanjeInventurnihZaliha.rdlc')
$backup=Join-Path $WorkDirectory 'installed_before';$configs=@{};$copied=@()
foreach($target in $targets){
 $exe=Join-Path $target.Path 'Materijalno.UI.exe'
 if(@(Get-Process -Name 'Materijalno.UI' -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq $exe}).Count){throw ('Close '+$exe+' before replacement.')}
 $config=$exe+'.config';$configs[$target.Name]=(Get-FileHash -LiteralPath $config).Hash
 $xml=[xml](Get-Content -LiteralPath $config -Raw)
 $catalog=([Data.SqlClient.SqlConnectionStringBuilder]::new([string](($xml.configuration.connectionStrings.add | Where-Object name -eq MaterijalnoDb).connectionString))).InitialCatalog
 if($target.Test -and $catalog -ne 'materijalno_fox_test_20261104_215755'){throw 'Unexpected test configuration.'}
 if(-not $target.Test -and $catalog -ne 'materijalno_fox_20261105_102038'){throw 'Unexpected working configuration.'}
 foreach($file in $files){
  $old=Join-Path $target.Path $file;$saved=Join-Path (Join-Path $backup $target.Name) $file
  if(-not(Test-Path -LiteralPath (Join-Path $BuildDirectory $file))){throw ('Missing verified file '+$file)}
  $null=New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force
  Copy-Item -LiteralPath $old -Destination $saved -Force
 }
 Copy-Item -LiteralPath $config -Destination (Join-Path (Join-Path $backup $target.Name) 'Materijalno.UI.exe.config') -Force
}
try {
 foreach($target in $targets){
  foreach($file in $files){
   $src=Join-Path $BuildDirectory $file;$dst=Join-Path $target.Path $file
   if([IO.Path]::GetFullPath($src) -ne [IO.Path]::GetFullPath($dst)){Copy-Item -LiteralPath $src -Destination $dst -Force}
   if((Get-FileHash -LiteralPath $src).Hash -ne (Get-FileHash -LiteralPath $dst).Hash){throw ('Hash mismatch '+$dst)}
  }
  if((Get-FileHash -LiteralPath ((Join-Path $target.Path 'Materijalno.UI.exe')+'.config')).Hash -ne $configs[$target.Name]){throw 'Database configuration changed.'}
  $copied+=@{Target=$target.Name;Path=$target.Path;Files=$files.Count;ConfigurationPreserved=$true}
 }
}catch{
 foreach($target in $targets){foreach($file in $files){Copy-Item -LiteralPath (Join-Path (Join-Path $backup $target.Name) $file) -Destination (Join-Path $target.Path $file) -Force}}
 throw
}
@{Backup=$backup;Targets=$copied;Source=$BuildDirectory;Completed=Get-Date -Format o} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'cost_inventory_deployment.json') -Encoding utf8
'PASS verified application and report fixes installed in FoxApp, Debug and PrometTest; configurations preserved.'
