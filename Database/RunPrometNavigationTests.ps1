param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\PrometNavigationQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_promet_navigation_run.txt')).Trim()
$exe=Join-Path $BuildDirectory 'PrometNavigationTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll $refs (Join-Path $PSScriptRoot 'PrometNavigationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Navigation tests compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
$material=$config.configuration.connectionStrings.add|Where-Object name -eq 'MaterijalnoDb'
$material.SetAttribute('connectionString','Server=127.0.0.1,9;Initial Catalog=disabled;Integrated Security=true;Connect Timeout=1')
$oracle=$config.configuration.connectionStrings.add|Where-Object name -eq 'OracleFinansije'
if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($config.configuration.appSettings.add|Where-Object key -eq 'OracleExportEnabled').SetAttribute('value','false')
$config.Save($exe+'.config')
& $exe (Join-Path $root 'Materijalno.UI/GlavniView.xaml') | Tee-Object -FilePath (Join-Path $work 'tests.log')
if($LASTEXITCODE -ne 0){throw 'Navigation tests failed.'}
