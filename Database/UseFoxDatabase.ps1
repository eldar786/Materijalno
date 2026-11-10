param([Parameter(Mandatory=$true)][ValidatePattern('^materijalno_fox_[0-9]{8}_[0-9]{6}$')][string]$DatabaseName)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$oracleSource=Get-Content -LiteralPath (Join-Path $project 'Materijalno.ViewModel\NaloziMedjuskladisnicaViewModel.cs') -Raw
$match=[regex]::Match($oracleSource,'string\s+connectionString\s*=\s*"([^"]+)"')
foreach($file in @('Materijalno.UI\App.config','Materijalno.Model\App.config','Materijalno.ViewModel\app.config')){
 $p=Join-Path $project $file;$xml=[xml](Get-Content -LiteralPath $p -Raw);$xml.PreserveWhitespace=$true
 $entry=$xml.SelectSingleNode("/configuration/connectionStrings/add[@name='MaterijalnoDb']")
 if($entry){$entry.SetAttribute('connectionString',[regex]::Replace($entry.GetAttribute('connectionString'),'(?i)(Database|Initial Catalog)\s*=\s*[^;]*',('$1='+$DatabaseName)))}
 if($file -eq 'Materijalno.UI\App.config'){
  if($match.Success -and -not $xml.SelectSingleNode("/configuration/connectionStrings/add[@name='OracleFinansije']")){$e=$xml.CreateElement('add');$e.SetAttribute('name','OracleFinansije');$e.SetAttribute('providerName','Oracle.ManagedDataAccess.Client');$e.SetAttribute('connectionString',$match.Groups[1].Value);$null=$xml.SelectSingleNode('/configuration/connectionStrings').AppendChild($e)}
  if(-not $xml.SelectSingleNode('/configuration/appSettings')){$e=$xml.CreateElement('appSettings');$null=$xml.DocumentElement.AppendChild($e)}
  $setting=$xml.SelectSingleNode("/configuration/appSettings/add[@key='OracleExportEnabled']")
  if(!$setting){$setting=$xml.CreateElement('add');$setting.SetAttribute('key','OracleExportEnabled');$null=$xml.SelectSingleNode('/configuration/appSettings').AppendChild($setting)}
  $setting.SetAttribute('value','false')
 }
 $settings=[System.Xml.XmlWriterSettings]::new();$settings.Indent=$true;$settings.Encoding=[Text.UTF8Encoding]::new($false);$writer=[Xml.XmlWriter]::Create($p,$settings);try{$xml.Save($writer)}finally{$writer.Dispose()}
}
Write-Output ('Aplikacija je povezana na '+$DatabaseName+'. Pristupni podaci nisu promijenjeni.')
