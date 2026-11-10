param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_(test_)?[0-9]{8}_[0-9]{6}$')][string]$DatabaseName)
$ErrorActionPreference='Stop'
$cfg=[xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Materijalno.UI\App.config') -Raw)
$b=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($cfg.configuration.connectionStrings.add | Where-Object name -eq 'MaterijalnoDb').connectionString))
$b['Initial Catalog']=$DatabaseName
$c=[Data.SqlClient.SqlConnection]::new($b.ConnectionString)
try {
 $c.Open();$cmd=$c.CreateCommand()
 $cmd.CommandText="IF NOT EXISTS(SELECT 1 FROM dbo.app_settings WHERE key_name='ImportComplete' AND value='true') THROW 51000,'Incomplete Fox import',1; IF COL_LENGTH('dbo.nalmat','line_number') IS NULL ALTER TABLE dbo.nalmat ADD line_number int NULL;"
 $null=$cmd.ExecuteNonQuery();'Fox schema ready: '+$DatabaseName
}finally{$c.Dispose()}
& (Join-Path $PSScriptRoot 'ImportFoxCounters.ps1') -DatabaseName $DatabaseName
