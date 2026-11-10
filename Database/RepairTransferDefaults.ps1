param([Parameter(Mandatory=$true)][string]$DatabaseName,[Parameter(Mandatory=$true)][string]$WorkDirectory,[switch]$Apply)
$ErrorActionPreference='Stop'
if($DatabaseName -notmatch '^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$'){throw 'Only a new Fox-derived application database is supported.'}
$root=Split-Path $PSScriptRoot -Parent
$config=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($config.configuration.connectionStrings.add|Where-Object name -eq MaterijalnoDb).connectionString))
$builder['Initial Catalog']=$DatabaseName
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
$null=New-Item -ItemType Directory -Path $WorkDirectory -Force
$columns=@('fcj','troskovi','cartro','zavtro','ppp','tarifa','fvrijed','porppp','trospe','ourst','mjtst')
$numericText=@('fcj','cartro','zavtro','ppp','fvrijed')
$missing=($columns|ForEach-Object{if($_ -in $numericText){"[$_] IS NULL OR LTRIM(RTRIM([$_])) = ''"}else{"[$_] IS NULL"}}) -join ' OR '
$where="status='M' AND fox_recno IS NULL AND is_archived=0 AND ($missing)"
$assign=($columns|ForEach-Object{if($_ -in $numericText){"[$_]=CASE WHEN [$_] IS NULL OR LTRIM(RTRIM([$_]))='' THEN '0' ELSE [$_] END"}elseif($_ -eq 'tarifa'){"[$_]=ISNULL([$_],'')"}else{"[$_]=ISNULL([$_],0)"}}) -join ', '
function Table([string]$sql,[Data.SqlClient.SqlTransaction]$transaction){
 $cmd=$connection.CreateCommand();$cmd.CommandTimeout=60;$cmd.CommandText=$sql;if($transaction){$cmd.Transaction=$transaction}
 $table=[Data.DataTable]::new();$adapter=[Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$adapter.Fill($table);return ,$table
}
function Digest([Data.DataTable]$table){
 $writer=[IO.StringWriter]::new([Globalization.CultureInfo]::InvariantCulture);$table.TableName='snapshot';$table.WriteXml($writer,[Data.XmlWriteMode]::WriteSchema)
 $sha=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($writer.ToString()))).Replace('-','')}finally{$sha.Dispose();$writer.Dispose()}
}
$transaction=$null
try{
 $connection.Open()
 $rows=Table "SELECT id,fox_recno,is_draft,brfak,redbr,$($columns -join ',') FROM dbo.mat WHERE $where ORDER BY id" $null
 $rows|Export-Clixml -LiteralPath (Join-Path $WorkDirectory 'original_defaults.xml')
 $beforeHash=$null;$afterHash=$null;$changed=0
 if($Apply -and $rows.Rows.Count -gt 0){
  # Use the same lock as posting, so pending UI saves cannot overlap the repair.
  $transaction=$connection.BeginTransaction([Data.IsolationLevel]::Serializable)
  $cmd=$connection.CreateCommand();$cmd.Transaction=$transaction;$cmd.CommandTimeout=60
  $cmd.CommandText="DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Materijalno:ledger',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000; IF @r<0 THROW 51000,N'Ledger is busy.',1;";[void]$cmd.ExecuteNonQuery()
  $protected='SELECT id,kljnaz,kljnaz1,ident,datun,analst,brdok,brnar,datnar,brfak,kolic,nc,vrijed,redbr,status,konto1,konto2,medus,kontosklad,kontosklad1,is_draft,is_archived,book_year,document_id,transfer_pair_id,line_token,fox_recno FROM dbo.mat ORDER BY id'
  $beforeHash=Digest (Table $protected $transaction)
  # Save the exact original fields under the lock before changing any values.
  $rows=Table "SELECT id,fox_recno,is_draft,brfak,redbr,$($columns -join ',') FROM dbo.mat WHERE $where ORDER BY id" $transaction
  $rows|Export-Clixml -LiteralPath (Join-Path $WorkDirectory 'original_defaults.xml')
  $cmd.CommandText="UPDATE dbo.mat SET $assign WHERE $where";$changed=$cmd.ExecuteNonQuery()
  $afterHash=Digest (Table $protected $transaction)
  if($beforeHash -ne $afterHash){throw 'Protected ledger fields changed.'}
  $remaining=Table "SELECT id FROM dbo.mat WHERE $where" $transaction
  if($remaining.Rows.Count -gt 0){throw 'Unnormalized new transfer fields remain.'}
  $transaction.Commit();$transaction.Dispose();$transaction=$null
 }
 $result=[ordered]@{Database=$DatabaseName;Apply=[bool]$Apply;Candidates=$rows.Rows.Count;Updated=$changed;ImportedRowsExcluded=$true;ProtectedFieldsUnchanged=($beforeHash -eq $afterHash);BeforeHash=$beforeHash;AfterHash=$afterHash;Columns=$columns}
 $result|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $WorkDirectory 'repair_result.json') -Encoding utf8
 [pscustomobject]$result|Select-Object Database,Apply,Candidates,Updated,ImportedRowsExcluded,ProtectedFieldsUnchanged
}catch{if($transaction){$transaction.Rollback()};throw}finally{if($transaction){$transaction.Dispose()};$connection.Dispose()}
