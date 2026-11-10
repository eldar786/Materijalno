param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$SourceFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM')
$ErrorActionPreference='Stop';$reference=Join-Path $WorkDirectory 'source';$reports=Join-Path $WorkDirectory 'fox_inventory'
$null=New-Item -ItemType Directory -Path $reports -Force
foreach($ext in @('frx','frt')){Copy-Item -LiteralPath (Join-Path $SourceFolder ('invzal.'+$ext)) -Destination (Join-Path $reports ('invzal.'+$ext)) -Force}
$fox=$null
function Fox([string]$cmd){try{$fox.DoCmd($cmd)}catch{throw ($cmd+': '+$_.Exception.Message)}}
try {
 $fox=New-Object -ComObject VisualFoxPro.Application.9;$fox.Visible=$false
 Fox 'SET RESOURCE OFF';Fox 'SET EXCLUSIVE OFF';Fox 'SET DELETED ON';Fox 'SET SAFETY OFF';Fox 'SET REPORTBEHAVIOR 80'
 foreach($name in @('mat','mater','inv','datnaz')){Fox ('USE "'+(Join-Path $reference ($name+'.dbf'))+'" IN 0 ALIAS '+$name+' SHARED NOUPDATE')}
 Fox 'SELECT mater';Fox 'SET ORDER TO TAG ident';Fox 'SELECT datnaz';Fox 'SET ORDER TO TAG kljnaz'
 Fox 'SELECT inv';Fox ('INDEX ON ident+kljnaz TO "'+(Join-Path $reports 'inv_order.idx')+'"')
 Fox 'SELECT mat';Fox ('INDEX ON ident+kljnaz TO "'+(Join-Path $reports 'mat_order.idx')+'"')
 Fox 'SET RELATION TO ident INTO mater, ident+kljnaz INTO inv, kljnaz INTO datnaz'
 Fox 'PUBLIC anazivorg,vu2';$results=@()
 foreach($warehouse in @(1000,1001,1002,1003)){
  Fox 'SELECT inv';Fox ('SET FILTER TO VAL(kljnaz)='+$warehouse);Fox 'SUM vrijed TO vu2'
  Fox 'SELECT mat';Fox ('SET FILTER TO VAL(kljnaz)='+$warehouse+' AND NOT EMPTY(ident)');Fox 'GO TOP';Fox 'anazivorg=datnaz.nazivorg'
  Fox ('REPORT FORM "'+(Join-Path $reports 'invzal.frx')+'" NOCONSOLE TO FILE "'+(Join-Path $reports ('invzal_'+$warehouse+'.txt'))+'" ASCII')
  Fox 'GO TOP';$previous='';$row=$null
  while(-not $fox.Eval('EOF()')){
   $ident=([string]$fox.Eval('mat.ident')).Trim()
   if($ident -ne $previous){if($row){$results+=$row};$row=[ordered]@{Warehouse=$warehouse;Material=$ident;Quantity=[decimal]0;Value=[decimal]0;Price=[decimal]0;InvQuantity=[decimal]0;InvValue=[decimal]0}}
   $row.Quantity += [decimal]$fox.Eval('mat.kolic');$row.Value += [decimal]$fox.Eval('mat.vrijed');$row.Price=[decimal]$fox.Eval('ROUND(mat.nc,9)')
   $row.InvQuantity=[decimal]$fox.Eval('IIF(inv.ident=mat.ident AND inv.kljnaz=mat.kljnaz,inv.kolic,0)')
   $row.InvValue=[decimal]$fox.Eval('IIF(inv.ident=mat.ident AND inv.kljnaz=mat.kljnaz,inv.vrijed,0)')
   $previous=$ident;Fox 'SKIP'
  }
  if($row){$results+=$row}
 }
 $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'fox_inventory_balances.json') -Encoding utf8
 'PASS original Fox inventory balance reports rendered; '+$results.Count+' materials captured in 4 warehouses.'
}finally{if($fox){$fox.Quit();[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)}}
