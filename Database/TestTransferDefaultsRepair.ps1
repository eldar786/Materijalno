$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_transfer_defaults_run.txt')).Trim()
$database=(Get-Content -LiteralPath (Join-Path $work 'database_name.txt')).Trim()
if($database -notmatch '^materijalno_fox_test_20261106_[0-9]{6}$'){throw 'Isolated test database required.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $root 'Materijalno.UI/App.config') -Raw)
$builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string](($config.configuration.connectionStrings.add|Where-Object name -eq MaterijalnoDb).connectionString));$builder['Initial Catalog']=$database
$connection=[Data.SqlClient.SqlConnection]::new($builder.ConnectionString);$id=$null
try{
 $connection.Open();$cmd=$connection.CreateCommand()
 $cmd.CommandText="INSERT dbo.mat(is_draft,is_archived,book_year,line_token,status,brfak,redbr,troskovi,ourst) OUTPUT inserted.id VALUES(1,0,2026,NEWID(),'M','QADEF-NULL',1,3.14,7)"
 $id=[int]$cmd.ExecuteScalar()
 $result=& (Join-Path $PSScriptRoot 'RepairTransferDefaults.ps1') -DatabaseName $database -WorkDirectory (Join-Path $work 'repair_regression') -Apply
 if($result.Updated -lt 1 -or -not $result.ProtectedFieldsUnchanged){throw 'Repair did not update test null values safely.'}
 $cmd.CommandText='SELECT fcj,troskovi,cartro,zavtro,ppp,tarifa,fvrijed,porppp,trospe,ourst,mjtst FROM dbo.mat WHERE id=@id';[void]$cmd.Parameters.AddWithValue('@id',$id)
 $table=[Data.DataTable]::new();$adapter=[Data.SqlClient.SqlDataAdapter]::new($cmd);[void]$adapter.Fill($table);$row=$table.Rows[0]
 foreach($column in $table.Columns){if($row.IsNull($column.ColumnName)){throw ('Remaining null: '+$column.ColumnName)}}
 if([decimal]$row.troskovi -ne 3.14 -or [int]$row.ourst -ne 7){throw 'Explicit nonzero values were overwritten.'}
 $repeat=& (Join-Path $PSScriptRoot 'RepairTransferDefaults.ps1') -DatabaseName $database -WorkDirectory (Join-Path $work 'repair_repeat') -Apply
 if($repeat.Updated -ne 0){throw 'Repair is not idempotent.'}
 'PASS repair fills missing fields, preserves explicit nonzero values and every protected ledger field; repeat changes zero rows.' | Tee-Object -FilePath (Join-Path $work 'repair_tests.log')
}finally{
 if($id){$cmd=$connection.CreateCommand();$cmd.CommandText="DELETE FROM dbo.mat WHERE id=@id AND brfak='QADEF-NULL' AND is_draft=1";[void]$cmd.Parameters.AddWithValue('@id',$id);[void]$cmd.ExecuteNonQuery()}
 $connection.Dispose()
}
