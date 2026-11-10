param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,[string]$FoxFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM',[string]$ConfigurationPath=(Join-Path $PSScriptRoot '..\Materijalno.UI\App.config'))
$ErrorActionPreference='Stop'
if(-not ('Materijalno.Import.DbfReader' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'DbfReader.cs')}
$cfg=[xml](Get-Content -LiteralPath $ConfigurationPath -Raw)
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb').connectionString))
$builder['Initial Catalog']=$DatabaseName
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$tx=$null
function Command([string]$sql){$c=$connection.CreateCommand();$c.Transaction=$tx;$c.CommandText=$sql;return $c}
try {
 $connection.Open();$tx=$connection.BeginTransaction([Data.IsolationLevel]::Serializable)
 $c=Command "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,'Counter import lock failed',1;";$null=$c.ExecuteNonQuery()
 foreach($name in @('broju','broji','brojm')) {
  $file=Join-Path $FoxFolder ($name+'.dbf');$table=[Materijalno.Import.DbfReader]::Read($file)
  $c=Command ("SELECT OBJECT_ID(N'fox_raw."+$name+"')")
  if($c.ExecuteScalar() -is [DBNull]) {
   $c=Command ('CREATE TABLE fox_raw.['+$name+'](fox_recno int NOT NULL PRIMARY KEY,fox_deleted bit NOT NULL,GODBRNA nvarchar(20) NOT NULL,BRNAL nvarchar(20) NOT NULL)');$null=$c.ExecuteNonQuery()
   foreach($record in $table.Records) {
    $c=Command ('INSERT fox_raw.['+$name+'] VALUES(@id,@deleted,@year,@number)')
    $null=$c.Parameters.AddWithValue('@id',$record.RecordNumber);$null=$c.Parameters.AddWithValue('@deleted',$record.Deleted);$null=$c.Parameters.AddWithValue('@year',[string]$record.Values['GODBRNA']);$null=$c.Parameters.AddWithValue('@number',[string]$record.Values['BRNAL']);$null=$c.ExecuteNonQuery()
   }
   $c=Command 'INSERT dbo.fox_import_manifest(table_name,physical_rows,active_rows,source_hash) VALUES(@name,@physical,@active,@hash)'
   $null=$c.Parameters.AddWithValue('@name',$name);$null=$c.Parameters.AddWithValue('@physical',$table.Records.Count);$null=$c.Parameters.AddWithValue('@active',@($table.Records | Where-Object {-not $_.Deleted}).Count);$null=$c.Parameters.AddWithValue('@hash',(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash);$null=$c.ExecuteNonQuery()
  }
  $counter=@{broju='PROMET_U';broji='PROMET_IV';brojm='PROMET_M'}[$name]
  foreach($record in $table.Records) {
   if($record.Deleted){continue};$yearText=([string]$record.Values['GODBRNA']).Trim();$numberText=([string]$record.Values['BRNAL']).Trim()
   if(!$yearText -or !$numberText){continue};$year=[int]$yearText;if($year -lt 100){$year+=2000};$number=[int]$numberText
   $c=Command 'UPDATE dbo.document_counter SET last_number=CASE WHEN last_number<@number THEN @number ELSE last_number END WHERE name=@name AND book_year=@year; IF @@ROWCOUNT=0 INSERT dbo.document_counter(name,book_year,last_number) VALUES(@name,@year,@number);'
   $null=$c.Parameters.AddWithValue('@name',$counter);$null=$c.Parameters.AddWithValue('@year',$year);$null=$c.Parameters.AddWithValue('@number',$number);$null=$c.ExecuteNonQuery()
  }
 }
 $tx.Commit();'Fox document counters preserved: '+$DatabaseName
}catch{if($tx){$tx.Rollback()};throw}finally{$connection.Dispose()}
