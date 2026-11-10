param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$SourceDatabase='materijalno_fox_20261105_102038')
$ErrorActionPreference='Stop'
if($SourceDatabase -notmatch '^materijalno_fox_[0-9]{8}_[0-9]{6}$'){throw 'Only the new working database may be copied.'}
$database='materijalno_fox_test_20261106_'+(Get-Date -Format 'HHmmss')
& (Join-Path $PSScriptRoot 'BackupFoxDatabase.ps1') -DatabaseName $SourceDatabase -ResultPath (Join-Path $WorkDirectory 'source_backup.json') | Out-Null
$backup=Get-Content -LiteralPath (Join-Path $WorkDirectory 'source_backup.json') -Raw | ConvertFrom-Json
if(-not $backup.Verified -or -not $backup.CopyOnly){throw 'Verified copy-only backup required.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config') -Raw)
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add | Where-Object name -eq MaterijalnoDb).connectionString))
$builder['Initial Catalog']='master';$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
try {
 $connection.Open();$cmd=$connection.CreateCommand();$cmd.CommandTimeout=180
 $cmd.CommandText='SELECT DB_ID(@name)';[void]$cmd.Parameters.AddWithValue('@name',$database)
 if($cmd.ExecuteScalar() -ne [DBNull]::Value){throw 'Test database already exists.'}
 $cmd.Parameters.Clear();$cmd.CommandText="SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))";$data=[string]$cmd.ExecuteScalar()
 $cmd.CommandText="SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))";$log=[string]$cmd.ExecuteScalar()
 if(-not $data -or -not $log){throw 'Default SQL file paths required.'}
 $cmd.CommandText='RESTORE FILELISTONLY FROM DISK=@backup';[void]$cmd.Parameters.AddWithValue('@backup',[string]$backup.ServerBackupFile)
 $table=[Data.DataTable]::new();$adapter=[Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$adapter.Fill($table)
 $moves=@();$n=0
 foreach($row in $table.Rows){
  $n++;$folder=if($row.Type -eq 'L'){$log}else{$data};$ext=if($row.Type -eq 'L'){'.ldf'}else{'.mdf'}
  $file=Join-Path $folder ($database+'_'+$n+$ext)
  $logical=([string]$row.LogicalName).Replace("'","''");$param='@file'+$n
  $moves+="MOVE N'$logical' TO $param";[void]$cmd.Parameters.AddWithValue($param,$file)
 }
 $cmd.CommandText='RESTORE DATABASE ['+$database+'] FROM DISK=@backup WITH '+($moves -join ',')+',RECOVERY,CHECKSUM;'
 [void]$cmd.ExecuteNonQuery()
 Set-Content -LiteralPath (Join-Path $WorkDirectory 'database_name.txt') -Value $database -Encoding utf8
 @{Source=$SourceDatabase;TestDatabase=$database;Backup=$backup.ServerBackupFile;OracleConnectionsUsed=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $WorkDirectory 'test_database.json') -Encoding utf8
 'PASS dedicated test copy created: '+$database
}finally{$connection.Dispose()}
