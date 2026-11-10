param([Parameter(Mandatory=$true)][string]$RunDirectory)
$ErrorActionPreference='Stop'
$manifest=Get-Content -Raw -LiteralPath (Join-Path $RunDirectory 'source\snapshot_manifest.json')|ConvertFrom-Json
function ReadHash([string]$path){$s=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite);$sha=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($sha.ComputeHash($s)).Replace('-','')}finally{$s.Dispose();$sha.Dispose()}}
foreach($file in $manifest.Files){if((ReadHash (Join-Path $manifest.Source $file.File)) -ne $file.SHA256){throw ('Production has changed since snapshot: '+$file.File)}}
$database=Get-Content -LiteralPath (Join-Path $RunDirectory 'database_name.txt')
if($database -notmatch '^materijalno_fox_[0-9]{8}_[0-9]{6}$'){throw 'Invalid new catalog.'}
$cfg=[xml](Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config'))
$b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add|Where-Object name -eq MaterijalnoDb).connectionString));$b['Initial Catalog']=$database
$c=[Data.SqlClient.SqlConnection]::new($b.ConnectionString);$fox=$null
try{
 $c.Open();$cmd=$c.CreateCommand();$cmd.CommandText='SELECT COUNT(*) posted,SUM(kolic) quantity,SUM(vrijed) value,MAX(datun) last_date FROM dbo.mat WHERE is_draft=0';$reader=$cmd.ExecuteReader();$null=$reader.Read();$sql=@{Rows=$reader.GetInt32(0);Quantity=$reader.GetDecimal(1);Value=$reader.GetDecimal(2);LastDate=$reader.GetDateTime(3)};$reader.Close()
 $errorFile=Join-Path $RunDirectory 'native_error.txt';if(Test-Path -LiteralPath $errorFile){throw 'Previous native error file exists.'}
 $fox=New-Object -ComObject VisualFoxPro.Application.9;$fox.Visible=$false
 $fox.DoCmd('SET RESOURCE OFF');$fox.DoCmd('SET EXCLUSIVE OFF');$fox.DoCmd('SET DELETED ON')
 $fox.DoCmd('ON ERROR =STRTOFILE(MESSAGE(),"'+$errorFile+'")')
 $sourceFile=Join-Path $manifest.Snapshot 'mat.dbf'
 $fox.DoCmd('USE "'+$sourceFile+'" IN 0 ALIAS mat SHARED NOUPDATE')
 if(Test-Path -LiteralPath $errorFile){throw 'Native Fox could not open the snapshot.'}
 if(-not [string]::Equals([IO.Path]::GetFullPath([string]$fox.Eval('DBF("mat")')),[IO.Path]::GetFullPath($sourceFile),[StringComparison]::OrdinalIgnoreCase)){throw 'Native engine opened a different table.'}
 $fox.DoCmd('SELECT mat');$fox.DoCmd('COUNT TO posted FOR VAL(ident)>0 AND VAL(kljnaz)>0');$fox.DoCmd('SUM kolic,vrijed TO q,v FOR VAL(ident)>0 AND VAL(kljnaz)>0')
 $native=@{Rows=[int]$fox.Eval('m.posted');Quantity=[decimal]$fox.Eval('m.q');Value=[decimal]$fox.Eval('m.v')}
 if($native.Rows -ne $sql.Rows -or $native.Quantity -ne $sql.Quantity -or [Math]::Round($native.Value,2) -ne $sql.Value){throw 'Native Fox / SQL ledger totals differ.'}
 $cmd.CommandText='SELECT COUNT(*) FROM dbo.mat WHERE ident IN(990201,990202)';if([int]$cmd.ExecuteScalar() -ne 0){throw 'Test materials leaked into production import.'}
 $result=@{Database=$database;ProductionSourceUnchanged=$true;SourceFilesChecked=$manifest.Files.Count;NativeFox=$native;Sql=$sql;TestMaterialsAbsent=$true;Passed=$true}
 $result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $RunDirectory 'native_and_source_verification.json') -Encoding utf8
 'PASS production files unchanged; native Fox / SQL posted counts, quantities and values agree; no test materials.'
}finally{if($fox){$fox.Quit();[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)};$c.Dispose()}
