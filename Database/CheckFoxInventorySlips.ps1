param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$SourceFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM BackUp 16-10-2026',[string]$ReferenceFolder)
$ErrorActionPreference='Stop'
$source=$SourceFolder
if(-not $ReferenceFolder){$ReferenceFolder=(Get-Content -Raw -LiteralPath (Join-Path $WorkDirectory 'fox_comparison.json') | ConvertFrom-Json).ReferenceFolder}
$folder=Join-Path $WorkDirectory 'inventory_reference'
$null=New-Item -ItemType Directory -Path $folder -Force
foreach($ext in @('frx','frt')){Copy-Item -LiteralPath (Join-Path $source ('invl.'+$ext)) -Destination (Join-Path $folder ('invl.'+$ext)) -Force}
$fox=$null
try{
 $fox=New-Object -ComObject VisualFoxPro.Application.9;$fox.Visible=$false
 $fox.DoCmd('SET RESOURCE OFF')
 $fox.DoCmd('SET EXCLUSIVE OFF');$fox.DoCmd('SET DELETED ON');$fox.DoCmd('SET SAFETY OFF');$fox.DoCmd('SET REPORTBEHAVIOR 80')
 foreach($name in @('mat','mater')){$fox.DoCmd('USE "'+(Join-Path $ReferenceFolder ($name+'.dbf'))+'" IN 0 ALIAS '+$name+' SHARED NOUPDATE')}
 $fox.DoCmd('SELECT mater');$fox.DoCmd('SET ORDER TO TAG ident');$fox.DoCmd('SELECT mat');$fox.DoCmd('SET ORDER TO TAG ident');$fox.DoCmd('SET RELATION TO ident INTO mater')
 $fox.DoCmd('PUBLIC anazivorg');$fox.DoCmd('anazivorg="FOX INVENTURNI LISTICI - PROVJERA"')
 $results=@()
 foreach($warehouse in @(1000,1001,1002)){
  $fox.DoCmd('SELECT mat');$fox.DoCmd('SET FILTER TO VAL(kljnaz)='+$warehouse);$fox.DoCmd('GO TOP')
  $textFile=Join-Path $folder ('invl_'+$warehouse+'.txt')
  $fox.DoCmd('REPORT FORM "'+(Join-Path $folder 'invl.frx')+'" NOCONSOLE TO FILE "'+$textFile+'" ASCII')
  # The FRX prints nc in the material group footer, without a/b conversion.
  $fox.DoCmd('GO TOP');$previous='';$last=$null
  while(-not $fox.Eval('EOF()')){
   $ident=([string]$fox.Eval('mat.ident')).Trim()
   if($previous -ne '' -and $ident -ne $previous){$results+=$last}
   $last=[ordered]@{Warehouse=$warehouse;Material=$ident;Price=[decimal]$fox.Eval('mat.nc');Name=([string]$fox.Eval('mater.nazmat')).Trim();Unit=([string]$fox.Eval('mater.jedm')).Trim();FoxRecno=[int]$fox.Eval('RECNO()')}
   $previous=$ident;$fox.DoCmd('SKIP')
  }
  if($last){$results+=$last}
 }
 $results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $WorkDirectory 'fox_inventory_slips.json') -Encoding utf8
 'Fox original inventory slip reports rendered to ASCII; '+$results.Count+' printed material groups captured.'
}finally{if($fox){$fox.Quit();[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)}}
