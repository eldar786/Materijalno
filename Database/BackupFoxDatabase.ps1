param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$')][string]$DatabaseName,[string]$ResultPath)
$ErrorActionPreference='Stop'
$cfg=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config') -Raw)
$b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb').connectionString))
$b['Initial Catalog']=$DatabaseName
$c=[Data.SqlClient.SqlConnection]::new($b.ConnectionString)
try {
 $c.Open();$cmd=$c.CreateCommand()
 $cmd.CommandText="SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"
 $folder=[string]$cmd.ExecuteScalar()
 if(!$folder){throw 'SQL Server nema određen direktorij za backup.'}
 $file=$folder.TrimEnd('\')+'\'+$DatabaseName+'_'+[DateTime]::Now.ToString('yyyyMMdd_HHmmss')+'.bak'
 $cmd.CommandTimeout=180
 $cmd.CommandText='BACKUP DATABASE ['+$DatabaseName+'] TO DISK=@file WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM;'
 $null=$cmd.Parameters.Add('@file',[Data.SqlDbType]::NVarChar,4000);$cmd.Parameters['@file'].Value=$file
 $null=$cmd.ExecuteNonQuery()
 $result=@{Database=$DatabaseName;ServerBackupFile=$file;Verified=$true;CopyOnly=$true} | ConvertTo-Json
 if($ResultPath){[IO.File]::WriteAllText($ResultPath,$result,[Text.UTF8Encoding]::new($false))}
 $result
}finally{$c.Dispose()}
