param(
 [Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,
 [string]$FoxFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM',
 [string]$ResultPath
)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'DbfReader.cs')
$cfg=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config') -Raw)
$builder=[System.Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb').connectionString))
$builder['Initial Catalog']=$DatabaseName
$builder['Connect Timeout']=15
$connection=[System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$failures=[Collections.Generic.List[string]]::new()
$results=@()
function Rows([string]$sql) {
 $command=$connection.CreateCommand();$command.CommandText=$sql
 $adapter=[Data.SqlClient.SqlDataAdapter]::new($command);$table=[Data.DataTable]::new()
 try {$null=$adapter.Fill($table);return ,$table}finally{$adapter.Dispose();$command.Dispose()}
}
function Different($left,$right,[type]$type) {
 $text=([string]$left).Trim()
 if($right -is [DBNull]) {return ($text -ne '')}
 if($type -eq [DateTime]) {return ($text -ne ([DateTime]$right).ToString('yyyyMMdd'))}
 if($type -in @([decimal],[int],[long],[double])) {
  if(!$text){return ([decimal]$right -ne 0)}
  return ([decimal]::Parse($text,[Globalization.CultureInfo]::InvariantCulture) -ne [decimal]$right)
 }
 return (-not [string]::Equals($text,[string]$right,[StringComparison]::Ordinal))
}
try {
 $connection.Open()
 foreach($name in @('mat','inv','nalmat','mater','kontosklad','datnaz','skontdat','brojnal','broju','broji','brojm')) {
  $source=[Materijalno.Import.DbfReader]::Read((Join-Path $FoxFolder ($name+'.dbf')))
  $raw=Rows ('SELECT * FROM fox_raw.['+$name+'] ORDER BY fox_recno')
  if($raw.Rows.Count -ne $source.Records.Count){$failures.Add($name+': physical count')}
  $checked=0
  foreach($record in $source.Records) {
   $r=$raw.Rows[$record.RecordNumber-1]
   if([int]$r.fox_recno -ne $record.RecordNumber -or [bool]$r.fox_deleted -ne $record.Deleted){$failures.Add($name+': deletion/record '+$record.RecordNumber)}
   foreach($field in $source.Fields) {
    $checked++
    if(-not [string]::Equals([string]$record.Values[$field.Name],[string]$r[$field.Name],[StringComparison]::Ordinal)){$failures.Add($name+': raw '+$record.RecordNumber+'/'+$field.Name)}
   }
  }
  $target=@{mat='mat';inv='inv';nalmat='nalmat';mater='tabela_materijala';kontosklad='sifarnik_materijal_skladiste_konto';datnaz='sifarnik_skladista';skontdat='sifarnik_konta'}[$name]
  $typedChecked=0;$duplicates=0
  if($target) {
   $typed=Rows ('SELECT * FROM dbo.['+$target+']')
   $byId=@{};foreach($r in $typed.Rows){$byId[[int]$r.id]=$r}
   $seen=@{};$expectedCount=0
   foreach($record in $source.Records) {
    if($record.Deleted){continue}
    if($name -eq 'kontosklad') {
     $key=([string]$record.Values['SIFMAT']).Trim()+'|'+([string]$record.Values['SIFSKLA']).Trim()
     if($seen.ContainsKey($key)){$duplicates++;continue};$seen[$key]=$true
    }
    $expectedCount++;$r=$byId[$record.RecordNumber]
    if($null -eq $r){$failures.Add($name+': missing typed '+$record.RecordNumber);continue}
    foreach($field in $source.Fields) {
     $column=$field.Name.ToLowerInvariant()
     if($column -eq 'kontoskla2'){$column='kontosklad1'}
     if($name -eq 'datnaz') {
      $map=@{nazivorg='naziv_org';zirorcun='ziro_racun';pozbr='poz_br';devrac='devizni_racun';pozbrd='poz_br_d';nazivsdk='naziv_sdk';mjeadr='mjesto_adresa'}
      if($map.ContainsKey($column)){$column=$map[$column]}
     }
     if(!$typed.Columns.Contains($column)){$failures.Add($name+': missing column '+$field.Name);continue}
     $typedChecked++
     if(Different $record.Values[$field.Name] $r[$column] $typed.Columns[$column].DataType){$failures.Add($name+': typed '+$record.RecordNumber+'/'+$field.Name)}
    }
   }
   if($expectedCount -ne $typed.Rows.Count){$failures.Add($name+': typed count')}
  }
  $results+=@{Table=$name;PhysicalRecords=$source.Records.Count;RawFieldsCompared=$checked;OperationalFieldsCompared=$typedChecked;IdenticalLinksDeduplicated=$duplicates}
 }
 $summary=@{Database=$DatabaseName;Passed=($failures.Count -eq 0);Tables=$results;FailureCount=$failures.Count;Failures=@($failures | Select-Object -First 30)}
 $json=$summary | ConvertTo-Json -Depth 5
 if($ResultPath){[IO.File]::WriteAllText($ResultPath,$json,[Text.UTF8Encoding]::new($false))}
 $json
 if($failures.Count -gt 0){throw 'DBF/SQL verification failed; see report.'}
}finally{$connection.Dispose()}
