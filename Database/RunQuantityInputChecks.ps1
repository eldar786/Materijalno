param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\QuantityInputQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_quantity_input_run.txt')).Trim()
$exe=Join-Path $BuildDirectory 'QuantityInputChecks.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Core.dll $refs (Join-Path $PSScriptRoot 'QuantityInputChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Quantity checks compilation failed.'}
$cfg=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
($cfg.configuration.connectionStrings.add|Where-Object name -eq 'MaterijalnoDb').SetAttribute('connectionString','Server=127.0.0.1,9;Initial Catalog=disabled;Integrated Security=true;Connect Timeout=1')
$oracle=$cfg.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije';if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($cfg.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$cfg.Save($exe+'.config')
& $exe | Tee-Object -FilePath (Join-Path $work 'checks.log')
if($LASTEXITCODE -ne 0){throw 'Quantity input checks failed.'}
