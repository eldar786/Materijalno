param([Parameter(Mandatory=$true)][string]$WorkDirectory,[string]$SourceFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM BackUp 16-10-2026')
$ErrorActionPreference='Stop'
$copyRoot=Join-Path 'D:\Materijalno Novo\artifacts\FoxReference' (Get-Date -Format 'yyyyMMdd_HHmmss')
$null=New-Item -ItemType Directory -Path $copyRoot -Force
$names=@('mat','mater','kontosklad','komat','broju','broji','brojm')
$hashes=@{}
foreach($name in $names) {
 $source=Join-Path $SourceFolder ($name+'.dbf');$hashes[$name]=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
 $file=Join-Path $copyRoot ($name+'.dbf');Copy-Item -LiteralPath $source -Destination $file
 # Make the copy a free table. Only the copied header's DBC backlink changes;
 # record bytes and original structural index are retained unchanged.
 $bytes=[IO.File]::ReadAllBytes($file);$header=[BitConverter]::ToUInt16($bytes,8)
 if($header -lt 296){throw ('Unexpected Fox header: '+$name)}
 [Array]::Clear($bytes,$header-263,263);[IO.File]::WriteAllBytes($file,$bytes)
 $index=Join-Path $SourceFolder ($name+'.cdx');if(Test-Path -LiteralPath $index){Copy-Item -LiteralPath $index -Destination (Join-Path $copyRoot ($name+'.cdx'))}
}
$hostFile=Join-Path $copyRoot 'reference_host.prg'
$code=[IO.File]::ReadAllText((Join-Path $WorkDirectory 'reference_host.prg'))
$errorMethod="PROCEDURE Error`r`nLPARAMETERS nError,cMethod,nLine`r`n=STRTOFILE(TRANSFORM(nError)+' '+MESSAGE()+' '+cMethod+' '+TRANSFORM(nLine),m.referenceErrorFile)`r`nENDPROC`r`n"
$code=$code.Replace('DEFINE CLASS ReferenceHost AS Form',('DEFINE CLASS ReferenceHost AS Form'+"`r`n"+$errorMethod))
[IO.File]::WriteAllBytes($hostFile,[Text.Encoding]::GetEncoding(1250).GetBytes($code))
$fox=$null;$results=@();$groups=@{}
function FoxCmd([string]$command){[IO.File]::WriteAllText((Join-Path $WorkDirectory 'fox_stage.txt'),$command);$fox.DoCmd($command);if(Test-Path -LiteralPath (Join-Path $copyRoot 'reference_error.txt')){throw [IO.File]::ReadAllText((Join-Path $copyRoot 'reference_error.txt'))}}
function N($value){return ([decimal]$value).ToString('0.#################',[Globalization.CultureInfo]::InvariantCulture)}
try {
 $fox=New-Object -ComObject VisualFoxPro.Application.9;$fox.Visible=$false
 FoxCmd 'SET TALK OFF';FoxCmd 'SET SAFETY OFF';FoxCmd 'SET RESOURCE OFF';FoxCmd 'SET DELETED ON';FoxCmd 'SET EXACT ON';FoxCmd 'SET EXCLUSIVE ON'
 FoxCmd ('SET DEFAULT TO ['+$copyRoot+']')
 FoxCmd ('SET PROCEDURE TO ['+$hostFile+'] ADDITIVE')
 FoxCmd 'PUBLIC referenceErrorFile';FoxCmd ('referenceErrorFile=['+(Join-Path $copyRoot 'reference_error.txt')+']')
 FoxCmd 'PUBLIC oRef';FoxCmd 'oRef=CREATEOBJECT("ReferenceHost")'
 foreach($control in (Get-Content -LiteralPath (Join-Path $WorkDirectory 'fox_control_paths.json') -Raw | ConvertFrom-Json)) {
  $parts=$control.Split('.');$current='oRef'
  for($i=0;$i -lt $parts.Length-1;$i++) {
   if(-not $fox.Eval(('PEMSTATUS('+ $current + ',"'+$parts[$i]+'",5)'))){FoxCmd ('=ADDPROPERTY('+$current+',"'+$parts[$i]+'",CREATEOBJECT("ReferenceNode"))')}
   $current+='.'+$parts[$i]
  }
 }
 FoxCmd 'CREATE DATABASE reference_data'
 FoxCmd 'ADD TABLE mat.dbf NAME mat'
 FoxCmd 'ALTER TABLE mat RENAME COLUMN KONTOSKLA2 TO KONTOSKLAD1'
 FoxCmd 'CLOSE TABLES ALL'
 foreach($name in $names){FoxCmd ('USE ['+(Join-Path $copyRoot ($name+'.dbf'))+'] IN 0 ALIAS '+$name+' EXCLUSIVE')}
 $actual=[string]$fox.Eval('DBF("mat")');if(-not $actual.StartsWith($copyRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Fox reference escaped its copy.'}
 FoxCmd 'SELECT mater';FoxCmd 'LOCATE FOR ALLTRIM(ident)=="13"';FoxCmd 'SCATTER NAME templateMaterial MEMO'
 foreach($ident in @('990201','990202')) {
  FoxCmd ('templateMaterial.ident="'+$ident+'"');FoxCmd ('templateMaterial.nazmat="E2E PONDER '+$(if($ident -eq '990201'){'ALFA'}else{'BETA'})+'"');FoxCmd 'INSERT INTO mater FROM NAME templateMaterial'
  foreach($warehouse in @('1000','1001','1002')) {
   FoxCmd 'SELECT kontosklad';FoxCmd ('LOCATE FOR ALLTRIM(sifmat)=="13" AND ALLTRIM(sifskla)=="'+$warehouse+'"')
   FoxCmd 'SCATTER NAME templateLink MEMO';FoxCmd ('templateLink.sifmat="'+$ident+'"');FoxCmd 'INSERT INTO kontosklad FROM NAME templateLink'
  }
 }
 $cases=Get-Content -LiteralPath (Join-Path $WorkDirectory 'scenario_saved.json') -Raw | ConvertFrom-Json
 foreach($item in $cases) {
  FoxCmd 'SELECT mat';FoxCmd 'SET FILTER TO';FoxCmd 'GO BOTTOM';FoxCmd 'SCATTER MEMVAR MEMO BLANK'
  FoxCmd ('SET ORDER TO TAG '+$(if($item.Status -eq 'M'){'kljnaz'}else{'brfak'}))
  foreach($property in @{ident=[string]$item.Ident;kljnaz=[string]$item.Warehouse;kljnaz1=$(if($item.Destination){[string]$item.Destination}else{''});brdok=('E2E-'+$item.Group);brnar=$(if($item.Status -eq 'M'){''}else{$item.WorkOrder});analst=$(if($item.Status -eq 'U'){'1RS03'}else{''});medus=''}.GetEnumerator()){FoxCmd ('m.'+$property.Key+'="'+$property.Value+'"')}
  FoxCmd ('m.datun={^'+$item.Date+'}');FoxCmd ('m.datnar={^'+$(if($item.Status -eq 'M'){$item.Date}else{$item.DocumentDate})+'}')
  foreach($property in @{kolic=$item.Quantity;fvrijed=$item.Invoice;cartro=$item.Customs;porppp=$item.Vat;zavtro=$item.Other;trospe=$item.Freight;redbr=$item.Line}.GetEnumerator()){FoxCmd ('m.'+$property.Key+'='+ (N $property.Value))}
  FoxCmd 'PUBLIC isadding,isediting,akonto1,akonto2,asiftar';FoxCmd 'isadding=.T.';FoxCmd 'isediting=.T.'
  FoxCmd 'SELECT mater';FoxCmd ('LOCATE FOR ALLTRIM(ident)=="'+$item.Ident+'"');FoxCmd 'akonto1=mater.konto1';FoxCmd 'akonto2=mater.konto2';FoxCmd 'asiftar=mater.siftar'
  FoxCmd 'SELECT kontosklad';FoxCmd 'LOCATE FOR sifmat=m.ident AND sifskla=m.kljnaz';FoxCmd 'm.kontosklad=kontosklad.sifkonta'
  if($item.Destination){FoxCmd 'LOCATE FOR sifmat=m.ident AND sifskla=m.kljnaz1';FoxCmd 'm.kontosklad1=kontosklad.sifkonta'}
  FoxCmd 'SELECT mat'
  $prefix=@{U='Receipt';I='Issue';M='Transfer';V='Return'}[$item.Status]
  if(!$groups.ContainsKey($item.Group)){FoxCmd ('oRef.'+$prefix+'Number()');$number=[string]$fox.Eval('m.brfak');$groups[$item.Group]=$number;if($number.Trim() -ne $item.Number){throw ('Fox/WPF number mismatch: '+$number+' / '+$item.Number)}}else{FoxCmd ('m.brfak="'+$groups[$item.Group]+'"')}
  if($item.Status -eq 'U'){FoxCmd 'oRef.ReceiptInvoice()'}
  FoxCmd ('oRef.'+$prefix+'Cost()')
  $price=[decimal]$fox.Eval('m.nc');$value=[decimal]$fox.Eval('m.vrijed')
  FoxCmd ('oRef.Save'+$prefix+'()')
  $results+=@{Index=$item.Index;Status=$item.Status;Number=$item.Number;Line=$item.Line;Price=$price;Value=$value;WpfPrice=$item.SavedPrice;WpfValue=[Math]::Abs([decimal]$item.SavedValue);PriceMatches=($price -eq [decimal]$item.SavedPrice);ValueMatches=($value -eq [Math]::Abs([decimal]$item.SavedValue))}
  'Fox saved '+$item.Number+'/'+$item.Line+' NC='+$price.ToString('F9')+' value='+$value
 }
 FoxCmd 'SELECT mat';FoxCmd 'SET FILTER TO';FoxCmd 'SET ORDER TO'
 FoxCmd 'SET FILTER TO INLIST(ALLTRIM(ident),"990201","990202")';FoxCmd 'GO TOP'
 $savedRows=@();$columns=@('kljnaz','kljnaz1','ident','datun','analst','brdok','brnar','datnar','brfak','kolic','nc','vrijed','redbr','status','konto1','konto2','fcj','troskovi','cartro','zavtro','ppp','tarifa','fvrijed','porppp','medus','kontosklad','kontosklad1','trospe','ourst','mjtst')
 while(-not $fox.Eval('EOF()')) {
  $row=[ordered]@{FoxRecno=[int]$fox.Eval('RECNO()')}
  foreach($column in $columns){$type=[string]$fox.Eval('TYPE("mat.'+$column+'")');$row[$column]=if($type -eq 'D'){([string]$fox.Eval('DTOC(mat.'+$column+',1)')).Trim()}else{$value=$fox.Eval('mat.'+$column);if($value -is [string]){$value.Trim()}else{$value}}}
  $savedRows+=$row;FoxCmd 'SKIP'
 }
 [IO.File]::WriteAllText((Join-Path $WorkDirectory 'fox_saved_rows.json'),($savedRows | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
 $version=[string]$fox.Eval('VERSION()')
 FoxCmd 'CLOSE ALL'
 foreach($name in $names){if((Get-FileHash -LiteralPath (Join-Path $SourceFolder ($name+'.dbf')) -Algorithm SHA256).Hash -ne $hashes[$name]){throw ('Original file changed: '+$name)}}
 $result=@{Engine=$version;ReferenceFolder=$copyRoot;OriginalFilesUnchanged=$true;Procedures='Unchanged Fox SCX methods; visual controls replaced by noninteractive stubs';Cases=$results;PriceMismatches=@($results | Where-Object {-not $_.PriceMatches}).Count;ValueMismatches=@($results | Where-Object {-not $_.ValueMatches}).Count}
 [IO.File]::WriteAllText((Join-Path $WorkDirectory 'fox_comparison.json'),($result | ConvertTo-Json -Depth 7),[Text.UTF8Encoding]::new($false))
 $result | Select-Object Engine,ReferenceFolder,OriginalFilesUnchanged,PriceMismatches,ValueMismatches | ConvertTo-Json
}finally{if($fox){try{$fox.Quit()}finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($fox)}}}
