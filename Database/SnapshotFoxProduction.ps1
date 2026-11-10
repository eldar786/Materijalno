param([Parameter(Mandatory=$true)][string]$SourceFolder,[Parameter(Mandatory=$true)][string]$DestinationFolder)
$ErrorActionPreference='Stop'
$sourceRoot=(Resolve-Path -LiteralPath $SourceFolder).Path.TrimEnd('\')
$destination=[IO.Path]::GetFullPath($DestinationFolder)
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')).TrimEnd('\')+'\'
if(-not $destination.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase)){throw 'Snapshot must stay in the workspace.'}
if(Test-Path -LiteralPath $destination){throw 'Snapshot destination already exists.'}
$extensions=@('.dbf','.fpt','.dbt','.cdx','.idx','.dbc','.dct','.dcx')
$files=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {$_.Extension.ToLowerInvariant() -in $extensions} | Sort-Object FullName)
function HashRead([string]$path){
 $s=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 $sha=[Security.Cryptography.SHA256]::Create()
 try{return [BitConverter]::ToString($sha.ComputeHash($s)).Replace('-','')}finally{$sha.Dispose();$s.Dispose()}
}
$before=@{};foreach($file in $files){$before[$file.FullName]=HashRead $file.FullName}
$null=New-Item -ItemType Directory -Path $destination
$manifest=@()
foreach($file in $files){
 $relative=$file.FullName.Substring($sourceRoot.Length+1);$target=Join-Path $destination $relative
 $null=New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
 $inputStream=[IO.FileStream]::new($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 $outputStream=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try{$inputStream.CopyTo($outputStream)}finally{$outputStream.Dispose();$inputStream.Dispose()}
 $copyHash=HashRead $target
 if($copyHash -ne $before[$file.FullName]){throw ('Source changed while copying: '+$relative)}
 $manifest+=@{File=$relative;Bytes=(Get-Item -LiteralPath $target).Length;SHA256=$copyHash;SourceLastWriteUtc=$file.LastWriteTimeUtc.ToString('o')}
}
foreach($file in $files){if((HashRead $file.FullName) -ne $before[$file.FullName]){throw ('Production changed during snapshot: '+$file.Name)}}
$after=@(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {$_.Extension.ToLowerInvariant() -in $extensions})
if($after.Count -ne $files.Count){throw 'Production data file membership changed during snapshot.'}
$result=@{Source=$sourceRoot;Snapshot=$destination;CapturedUtc=[DateTime]::UtcNow.ToString('o');SourceStable=$true;Files=$manifest}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $destination 'snapshot_manifest.json') -Encoding utf8
'Stable production snapshot: '+$manifest.Count+' data files; '+@($files|Where-Object Extension -eq '.dbf').Count+' DBF tables.'
