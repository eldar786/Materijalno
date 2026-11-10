param([Parameter(Mandatory=$true)][string]$WorkDirectory)
$ErrorActionPreference='Stop'
$comparison=Get-Content -LiteralPath (Join-Path $WorkDirectory 'fox_comparison.json') -Raw | ConvertFrom-Json
$fields=Get-Content -LiteralPath (Join-Path $WorkDirectory 'fox_dnevnik_fields.json') -Raw | ConvertFrom-Json
$rows=Get-Content -LiteralPath (Join-Path $WorkDirectory 'fox_saved_rows.json') -Raw | ConvertFrom-Json
$expressions=@{IncomingQuantity=29;OutgoingQuantity=30;Debit=34;Credit=35;Price=19}
$fox=$null
try{
    $fox=New-Object -ComObject VisualFoxPro.Application.9
    $fox.Visible=$false;$fox.DoCmd('SET EXCLUSIVE OFF');$fox.DoCmd('SET DELETED ON')
    $fox.DoCmd('USE "'+(Join-Path $comparison.ReferenceFolder 'mat.dbf')+'" IN 0 ALIAS mat SHARED NOUPDATE');$fox.DoCmd('SELECT mat')
    $result=@()
    foreach($row in $rows){
        $fox.DoCmd('GO '+$row.FoxRecno)
        $record=[ordered]@{Number=$row.brfak;Material=$row.ident;Warehouse=$row.kljnaz;Line=$row.redbr;Status=$row.status;Medus=$row.medus}
        foreach($key in $expressions.Keys){$expr=($fields | Where-Object Recno -eq $expressions[$key]).EXPR;$record[$key]=[decimal]$fox.Eval($expr)}
        if($row.status -eq 'V' -and ($record.IncomingQuantity -ne 0 -or $record.OutgoingQuantity -ne 0 -or $record.Debit -ne 0 -or $record.Credit -ne 0)){throw 'Unexpected Fox return expression result.'}
        $result+=$record
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'fox_dnevnik_values.json') -Encoding utf8
    'Fox original FRX expressions evaluated on '+$result.Count+' saved rows; all return movement columns equal zero.'
}finally{if($fox){$fox.Quit();[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)}}
