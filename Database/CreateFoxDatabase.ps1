param(
 [Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,
 [string]$FoxFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM',
 [string]$ConfigurationPath=(Join-Path $PSScriptRoot '..\Materijalno.UI\App.config'),
 [string]$ResultPath
)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'DbfReader.cs')
$cfg=[xml](Get-Content -LiteralPath $ConfigurationPath -Raw)
$cs=[string](($cfg.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb').connectionString)
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new($cs)
$builder['Initial Catalog']='master'
$builder['Connect Timeout']=10
$builder['Application Name']='Materijalno_Fox_Import_New_Database'
$master=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$connection=$null
function Exec([string]$sql){$c=$connection.CreateCommand();$c.CommandTimeout=120;$c.CommandText=$sql;try{$null=$c.ExecuteNonQuery()}finally{$c.Dispose()}}
function Scalar([string]$sql){$c=$connection.CreateCommand();$c.CommandTimeout=120;$c.CommandText=$sql;try{return $c.ExecuteScalar()}finally{$c.Dispose()}}
function Quote([string]$name){return '['+$name.Replace(']',']]')+']'}
function Text($v){if($null -eq $v){return ''};return ([string]$v).Trim()}
function Num($v){$s=Text $v;if(!$s){return $null};return [decimal]::Parse($s,[System.Globalization.NumberStyles]::Number,[System.Globalization.CultureInfo]::InvariantCulture)}
function Code($v){$n=Num $v;if($null -eq $n){return $null};return [int]$n}
function Day($v){$s=Text $v;if(!$s){return $null};return [DateTime]::ParseExact($s,'yyyyMMdd',[System.Globalization.CultureInfo]::InvariantCulture)}
function StableGuid([string]$key){$sha=[System.Security.Cryptography.SHA256]::Create();try{$bytes=$sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($key));$sixteen=[byte[]]::new(16);[Array]::Copy($bytes,$sixteen,16);return [Guid]::new($sixteen)}finally{$sha.Dispose()}}
function Bulk([string]$table,[object[]]$rows){
 if(!$rows -or $rows.Count -eq 0){return}
 $cmd=$connection.CreateCommand();$cmd.CommandText='SELECT TOP (0) * FROM '+$table
 $reader=$cmd.ExecuteReader();$types=@{};try{for($i=0;$i -lt $reader.FieldCount;$i++){$types[$reader.GetName($i)]=$reader.GetFieldType($i)}}finally{$reader.Close();$cmd.Dispose()}
 $dt=[System.Data.DataTable]::new();foreach($key in $rows[0].Keys){$null=$dt.Columns.Add($key,$types[$key])}
 foreach($item in $rows){$r=$dt.NewRow();foreach($key in $item.Keys){$r[$key]=if($null -eq $item[$key]){[DBNull]::Value}else{$item[$key]}};$dt.Rows.Add($r)}
 $options=[System.Data.SqlClient.SqlBulkCopyOptions]::KeepIdentity -bor [System.Data.SqlClient.SqlBulkCopyOptions]::CheckConstraints
 $bulk=[System.Data.SqlClient.SqlBulkCopy]::new($connection,$options,$null);$bulk.DestinationTableName=$table;$bulk.BulkCopyTimeout=120
 foreach($col in $dt.Columns){$null=$bulk.ColumnMappings.Add($col.ColumnName,$col.ColumnName)}
 try{$bulk.WriteToServer($dt)}finally{$bulk.Dispose();$dt.Dispose()}
}
try {
 $master.Open();$cmd=$master.CreateCommand();$cmd.CommandText='SELECT DB_ID(@name)';$null=$cmd.Parameters.AddWithValue('@name',$DatabaseName)
 $existing=$cmd.ExecuteScalar();$cmd.Dispose();if($existing -isnot [DBNull] -and $null -ne $existing){throw 'Odabrana baza već postoji. Postojeća baza se nikada ne prepisuje.'}
 $cmd=$master.CreateCommand();$cmd.CommandTimeout=120;$cmd.CommandText='CREATE DATABASE '+(Quote $DatabaseName);$null=$cmd.ExecuteNonQuery();$cmd.Dispose();$master.Close()
 $builder['Initial Catalog']=$DatabaseName;$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$connection.Open()
 Write-Output ('Nova baza: '+$DatabaseName)
 if($connection.Database -ne $DatabaseName -or $connection.Database -notmatch '^materijalno_fox_'){throw 'Zaustavljen pristup bazi izvan nove Fox baze.'}
 $schema=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Schema.sql') -Raw
 foreach($batch in [regex]::Split($schema,'(?im)^\s*GO\s*$')){if($batch.Trim()){Exec $batch}}
 Exec 'CREATE SCHEMA fox_raw'
 Write-Output 'Operativna shema je kreirana.'
 $tables=@{};$names=@('mat','inv','nalmat','mater','kontosklad','datnaz','skontdat','brojnal')
 foreach($name in $names){
  Write-Output ('DBF: '+$name)
  $table=[Materijalno.Import.DbfReader]::Read((Join-Path $FoxFolder ($name+'.dbf')));$tables[$name]=$table
  $cols=@('[fox_recno] int NOT NULL PRIMARY KEY','[fox_deleted] bit NOT NULL')
  foreach($f in $table.Fields){$cols+=((Quote $f.Name)+' nvarchar('+[Math]::Max(20,$f.Length)+') NOT NULL')}
  Exec ('CREATE TABLE fox_raw.'+(Quote $name)+' ('+($cols -join ',')+')')
  $rows=@(foreach($row in $table.Records){$r=[ordered]@{fox_recno=$row.RecordNumber;fox_deleted=$row.Deleted};foreach($f in $table.Fields){$r[$f.Name]=$row.Values[$f.Name]};$r})
  Bulk ('fox_raw.'+(Quote $name)) $rows
  $hash=(Get-FileHash -LiteralPath (Join-Path $FoxFolder ($name+'.dbf')) -Algorithm SHA256).Hash
  Bulk 'dbo.fox_import_manifest' @(@{table_name=$name;physical_rows=$table.Records.Count;active_rows=@($table.Records | Where-Object {-not $_.Deleted}).Count;source_hash=$hash})
 }
 $material=@(foreach($row in $tables.mater.Records){if($row.Deleted){continue};$v=$row.Values;@{Id=$row.RecordNumber;ident=(Code $v.IDENT);nazmat=(Text $v.NAZMAT);jedm=(Text $v.JEDM);siftar=(Code $v.SIFTAR);konto1=(Code $v.KONTO1);konto2=(Text $v.KONTO2)}})
 Bulk 'dbo.tabela_materijala' $material
 Bulk 'dbo.sifarnik_skladista' @(foreach($row in $tables.datnaz.Records){if($row.Deleted){continue};$v=$row.Values;@{id=$row.RecordNumber;kljnaz=(Code $v.KLJNAZ);naziv_org=(Text $v.NAZIVORG);ziro_racun=(Text $v.ZIRORCUN);poz_br=(Text $v.POZBR);devizni_racun=(Text $v.DEVRAC);poz_br_d=(Text $v.POZBRD);opstina=(Text $v.OPSTINA);naziv_sdk=(Text $v.NAZIVSDK);mjesto_adresa=(Text $v.MJEADR)}})
 Bulk 'dbo.sifarnik_konta' @(foreach($row in $tables.skontdat.Records){if($row.Deleted){continue};$v=$row.Values;@{id=$row.RecordNumber;sifkonta=(Code $v.SIFKONTA);nazkont=(Text $v.NAZKONT);stakont=(Code $v.STAKONT);stamt=(Code $v.STAMT)}})
 $seenLinks=@{};$links=@(foreach($row in $tables.kontosklad.Records){if($row.Deleted){continue};$v=$row.Values;$key=(Code $v.SIFMAT).ToString()+'|'+(Code $v.SIFSKLA).ToString();$account=Code $v.SIFKONTA;if($seenLinks.ContainsKey($key)){if($seenLinks[$key] -ne $account){throw ('Višeznačan izvorni konto za par '+$key)};continue};$seenLinks[$key]=$account;@{Id=$row.RecordNumber;Sifmat=(Code $v.SIFMAT);Sifskla=(Code $v.SIFSKLA);Sifkonta=$account}})
 Bulk 'dbo.sifarnik_materijal_skladiste_konto' $links
 $activeMat=@($tables.mat.Records | Where-Object {-not $_.Deleted})
 $year=($activeMat | ForEach-Object {if((Text $_.Values.DATUN).Length -eq 8){[int]$_.Values.DATUN.Substring(0,4)}} | Measure-Object -Maximum).Maximum
 $docs=@{};$pairs=@{};$matRows=@(foreach($row in $activeMat){
  $v=$row.Values;$posted=(Code $v.IDENT) -gt 0 -and (Code $v.KLJNAZ) -gt 0
  $date=Day $v.DATUN;$bookYear=if($date){$date.Year}else{[int]$year}
  $docId=$null;$pairId=$null
  if($posted){$warehouse=if($v.STATUS -eq 'M'){0}else{Code $v.KLJNAZ};$key=(Text $v.STATUS)+'|'+$bookYear+'|'+(Text $v.BRFAK)+'|'+$warehouse;$docId=StableGuid $key
   if(!$docs.ContainsKey($key)){$source=if($v.STATUS -eq 'M' -and (Num $v.KOLIC) -gt 0){Code $v.KLJNAZ1}else{Code $v.KLJNAZ};$docs[$key]=@{id=$docId;status=(Text $v.STATUS);book_year=$bookYear;number=(Text $v.BRFAK);warehouse=$source;created_at=[DateTime]::UtcNow}}
   if($v.STATUS -eq 'M'){$wh=@((Code $v.KLJNAZ),(Code $v.KLJNAZ1)) | Sort-Object;$pk=(Text $v.BRFAK)+'|'+(Text $v.IDENT)+'|'+(Text $v.DATUN)+'|'+($wh -join ':')+'|'+([Math]::Abs((Num $v.KOLIC))).ToString([System.Globalization.CultureInfo]::InvariantCulture)+'|'+([Math]::Abs((Num $v.VRIJED))).ToString([System.Globalization.CultureInfo]::InvariantCulture);$pairId=StableGuid $pk;if(!$pairs.ContainsKey($pk)){$pairs[$pk]=[Collections.Generic.List[object]]::new()};$pairs[$pk].Add($v)}
  }
  $r=[ordered]@{id=$row.RecordNumber;is_draft=(-not $posted);is_archived=$false;book_year=$bookYear;document_id=$docId;transfer_pair_id=$pairId;line_token=(StableGuid ('mat:'+ $row.RecordNumber));fox_recno=$row.RecordNumber}
  foreach($f in $tables.mat.Fields){$target=if($f.Name -eq 'KONTOSKLA2'){'kontosklad1'}else{$f.Name.ToLowerInvariant()};$value=$v[$f.Name]
   $r[$target]=if($f.Type -eq 'D'){Day $value}elseif($target -in @('kljnaz','kljnaz1','ident','konto1','kontosklad','kontosklad1','ourst','mjtst','redbr')){Code $value}elseif($target -in @('kolic','nc','vrijed','troskovi','porppp','trospe')){$n=Num $value;if($null -eq $n){[decimal]0}else{$n}}else{Text $value}
  };$r
 })
 foreach($pair in $pairs.Values){if($pair.Count -ne 2 -or (Num $pair[0].KOLIC)+(Num $pair[1].KOLIC) -ne 0 -or (Num $pair[0].VRIJED)+(Num $pair[1].VRIJED) -ne 0){throw 'Izvorna međuskladišnica nema uravnotežen par.'}}
 Bulk 'dbo.material_document' @($docs.Values)
 Bulk 'dbo.mat' $matRows
 $invRows=@(foreach($row in $tables.inv.Records){if($row.Deleted){continue};$r=[ordered]@{id=$row.RecordNumber;is_draft=((Code $row.Values.IDENT) -le 0);is_archived=$false;inventory_date=(Day $row.Values.DATUN);inventory_session_id=$null;fox_recno=$row.RecordNumber}
 foreach($f in $tables.inv.Fields){$target=if($f.Name -eq 'KONTOSKLA2'){'kontosklad1'}else{$f.Name.ToLowerInvariant()};$value=$row.Values[$f.Name];$r[$target]=if($f.Type -eq 'D'){Day $value}elseif($target -in @('kljnaz','ident','konto1','kontosklad','kontosklad1','ourst','mjtst','redbr')){Code $value}elseif($target -in @('kolic','nc','vrijed','troskovi','porppp','trospe')){$n=Num $value;if($null -eq $n){[decimal]0}else{$n}}else{Text $value}};$r})
 Bulk 'dbo.inv' $invRows
 $nalRows=@(foreach($row in $tables.nalmat.Records){if($row.Deleted){continue};$r=[ordered]@{id=$row.RecordNumber;fox_recno=$row.RecordNumber;batch_id=$null};foreach($f in $tables.nalmat.Fields){$target=$f.Name.ToLowerInvariant();$value=$row.Values[$f.Name];$r[$target]=if($target -in @('datnsta','datdokst')){Day $value}elseif($target -in @('rostav','sifakt','bronsta','sintstav','kukistav','ourst','mjtst')){$n=Code $value;if($null -eq $n -and $target -in @('rostav','sifakt','bronsta','sintstav','kukistav')){0}else{$n}}elseif($target -in @('kurs','dug1st','pot1st','stodug1','stopot1','devdugst','devpotst','stodevd','stodevp')){$n=Num $value;if($null -eq $n){[decimal]0}else{$n}}else{Text $value}};$r})
 Bulk 'dbo.nalmat' $nalRows
 Bulk 'dbo.app_settings' @(@{key_name='Source';value='FoxPro DBF import'},@{key_name='ActiveBookYear';value=[string]$year},@{key_name='ImportComplete';value='true'},@{key_name='DatabaseRole';value=$(if($DatabaseName -match '_test_'){'IntegrationTest'}else{'FoxApplication'})})
 & (Join-Path $PSScriptRoot 'ImportFoxCounters.ps1') -DatabaseName $DatabaseName -FoxFolder $FoxFolder -ConfigurationPath $ConfigurationPath
 $verification=[ordered]@{Database=$DatabaseName;Source='Original FoxPro DBF';ActiveBookYear=[int]$year;Tables=@();TransferPairs=$pairs.Count}
 foreach($name in $names){$active=@($tables[$name].Records | Where-Object {-not $_.Deleted}).Count;$actual=[long](Scalar ('SELECT COUNT_BIG(*) FROM fox_raw.'+(Quote $name)+' WHERE fox_deleted=0'));if($active -ne $actual){throw ('Broj redova se ne slaže: '+$name)};$verification.Tables+=@{Name=$name;Physical=$tables[$name].Records.Count;Active=$active;ImportedRaw=$actual}}
 $q=Scalar 'SELECT SUM(kolic) FROM dbo.mat WHERE is_draft=0';$v=Scalar 'SELECT SUM(vrijed) FROM dbo.mat WHERE is_draft=0';$verification.Quantity=$q.ToString([System.Globalization.CultureInfo]::InvariantCulture);$verification.Value=$v.ToString([System.Globalization.CultureInfo]::InvariantCulture)
 $verification.Materials=Scalar 'SELECT COUNT(*) FROM dbo.tabela_materijala';$verification.AccountLinks=Scalar 'SELECT COUNT(*) FROM dbo.sifarnik_materijal_skladiste_konto';$verification.Mat=Scalar 'SELECT COUNT(*) FROM dbo.mat';$verification.Nalmat=Scalar 'SELECT COUNT(*) FROM dbo.nalmat'
 if($ResultPath){$verification | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ResultPath -Encoding utf8}
 $verification | ConvertTo-Json -Depth 8
} catch {
 Write-Output ('ImportFailureType: '+$_.Exception.GetType().Name)
 Write-Output $_.ScriptStackTrace
 Write-Output $_.Exception.Message
 throw 'Import nove baze nije završen.'
} finally {if($connection){$connection.Dispose()};$master.Dispose()}
