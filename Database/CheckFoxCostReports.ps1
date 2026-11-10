param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$SourceFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM')
$ErrorActionPreference='Stop'
$reference=Join-Path $WorkDirectory 'source'
$reports=Join-Path $WorkDirectory 'fox_costs'
$null=New-Item -ItemType Directory -Path $reports -Force
foreach($name in @('kontrosv','kontona1')){foreach($ext in @('frx','frt')){Copy-Item -LiteralPath (Join-Path $SourceFolder ($name+'.'+$ext)) -Destination (Join-Path $reports ($name+'.'+$ext)) -Force}}
$fox=$null
function Fox([string]$cmd){try{$fox.DoCmd($cmd)}catch{throw ($cmd+': '+$_.Exception.Message)}}
try {
 $fox=New-Object -ComObject VisualFoxPro.Application.9;$fox.Visible=$false
 Fox 'SET RESOURCE OFF';Fox 'SET EXCLUSIVE OFF';Fox 'SET DELETED ON';Fox 'SET SAFETY OFF';Fox 'SET REPORTBEHAVIOR 80';Fox 'SET CENTURY ON';Fox 'SET DATE BRITISH'
 foreach($name in @('mat','mater','skontdat','datnaz')){Fox ('USE "'+(Join-Path $reference ($name+'.dbf'))+'" IN 0 ALIAS '+$name+' SHARED NOUPDATE')}
 foreach($pair in @(@('mater','ident'),@('skontdat','sifkonta'),@('datnaz','kljnaz'))){Fox ('SELECT '+$pair[0]);Fox ('SET ORDER TO TAG '+$pair[1])}
 Fox 'SELECT mat';Fox ('INDEX ON kljnaz+konto1+brfak TO "'+(Join-Path $reports 'qa_cost_order.idx')+'"')
 $ranges=@(@('Godina','2026-01-01','2026-11-05'),@('PrviKvartal','2026-01-01','2026-03-31'),@('TreciKvartal','2026-07-01','2026-09-30'))
 $results=@()
 foreach($range in $ranges){
  Fox ('PUBLIC adat1,adat2');Fox ('adat1={^'+$range[1]+'}');Fox ('adat2={^'+$range[2]+'}')
  Fox 'SELECT mat';Fox "SET FILTER TO status='I' AND datun>=adat1 AND datun<=adat2"
  Fox 'SET RELATION TO ident INTO mater, kljnaz INTO datnaz, konto1 INTO skontdat'
  foreach($report in @('kontrosv','kontona1')){
   Fox 'SELECT mat'
   if($report -eq 'kontona1'){Fox 'SET INDEX TO';Fox 'SET ORDER TO TAG Konto'}else{Fox ('SET INDEX TO "'+(Join-Path $reports 'qa_cost_order.idx')+'"')}
   Fox 'GO TOP'
   Fox ('REPORT FORM "'+(Join-Path $reports ($report+'.frx'))+'" NOCONSOLE TO FILE "'+(Join-Path $reports ($report+'_'+$range[0]+'.txt'))+'" ASCII')
  }
  Fox 'SELECT mat';Fox 'GO TOP'
  while(-not $fox.Eval('EOF()')){
   $results += [ordered]@{Range=$range[0];Warehouse=([string]$fox.Eval('mat.kljnaz')).Trim();Account=([string]$fox.Eval('mat.konto1')).Trim();MasterAccount=([string]$fox.Eval('mater.konto1')).Trim();Document=([string]$fox.Eval('mat.brfak')).Trim();DocumentDate=$fox.Eval('IIF(EMPTY(mat.datnar),"",DTOC(mat.datnar,1))');Material=([string]$fox.Eval('mat.ident')).Trim();Value=[decimal]$fox.Eval('mat.vrijed')}
   Fox 'SKIP'
  }
 }
 $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'fox_cost_movements.json') -Encoding utf8
 'PASS original Fox cost reports rendered for 3 periods; '+$results.Count+' reference movements captured.'
}finally{if($fox){$fox.Quit();[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)}}
