param([string]$TestDatabase='materijalno_fox_test_20261104_215755',[Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference='Stop'
if($TestDatabase -notmatch '^materijalno_fox_test_[0-9]{8}_[0-9]{6}$'){throw 'Test catalog required.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=Join-Path $workspace 'artifacts\PrometQA'
$main=Join-Path $workspace 'artifacts\FoxApp'
$test=Join-Path $workspace 'artifacts\PrometTest'
$backup=Join-Path $workspace ('artifacts\BeforePrometValidation_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
$mainConfig=[xml](Get-Content -LiteralPath (Join-Path $main 'Materijalno.UI.exe.config') -Raw)
$mainBuilder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($mainConfig.configuration.connectionStrings.add | Where-Object name -eq MaterijalnoDb).connectionString))
if($mainBuilder.InitialCatalog -notmatch '^materijalno_fox_[0-9]{8}_[0-9]{6}$'){throw 'Unexpected working catalog.'}
if(@(Get-Process -Name 'Materijalno.UI' -ErrorAction SilentlyContinue | Where-Object {$_.Path -eq (Join-Path $main 'Materijalno.UI.exe')}).Count){throw 'Close the working application before deployment.'}
$null=New-Item -ItemType Directory -Path $test,$backup -Force
$items=Get-ChildItem -LiteralPath $source | Where-Object {$_.Name -notmatch 'Tests\.(exe|pdb)' -and $_.Name -notlike '*.config'}
foreach($item in $items){
    $old=Join-Path $main $item.Name
    if(Test-Path -LiteralPath $old){Copy-Item -LiteralPath $old -Destination $backup -Recurse -Force}
    Copy-Item -LiteralPath $item.FullName -Destination $main -Recurse -Force
    Copy-Item -LiteralPath $item.FullName -Destination $test -Recurse -Force
}
foreach($file in Get-ChildItem -LiteralPath $source -Filter '*.config' | Where-Object {$_.Name -notmatch 'Tests\.exe\.config$'}){
    $configuration=[xml](Get-Content -LiteralPath $file.FullName -Raw)
    foreach($entry in $configuration.configuration.connectionStrings.add | Where-Object {$_ -and $_.name -ne 'OracleFinansije'}){
        $b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$entry.connectionString)
        $b['Initial Catalog']=$TestDatabase
        $entry.SetAttribute('connectionString',$b.ConnectionString)
    }
    foreach($entry in $configuration.configuration.appSettings.add | Where-Object key -eq OracleExportEnabled){$entry.SetAttribute('value','false')}
    $configuration.Save((Join-Path $test $file.Name))
}
$hashes=@()
foreach($name in @('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll')){
    $sourceHash=(Get-FileHash -LiteralPath (Join-Path $source $name)).Hash
    foreach($folder in @($main,$test)){
        if((Get-FileHash -LiteralPath (Join-Path $folder $name)).Hash -ne $sourceHash){throw ('Deployment hash mismatch: '+$name)}
    }
    $hashes+=@{File=$name;SHA256=$sourceHash}
}
$unchanged=[xml](Get-Content -LiteralPath (Join-Path $main 'Materijalno.UI.exe.config') -Raw)
if($unchanged.OuterXml -ne $mainConfig.OuterXml){throw 'Working configuration changed.'}
$result=@{WorkingDatabase=$mainBuilder.InitialCatalog;TestDatabase=$TestDatabase;Backup=$backup;Binaries=$hashes;WorkingConfigurationPreserved=$true}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'deployment_result.json') -Encoding utf8
'Deployed verified main and test applications; working database configuration preserved.'
