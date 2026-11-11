param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\StockFileQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_stock_file_run.txt')).Trim()
$output=Join-Path $BuildDirectory 'StockFileTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'StockFileTests.cs')
if($LASTEXITCODE -ne 0){throw 'Stock file tests compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
$oracle=$config.configuration.connectionStrings.add|Where-Object name -eq OracleFinansije;if($oracle){[void]$oracle.ParentNode.RemoveChild($oracle)}
($config.configuration.appSettings.add|Where-Object key -eq OracleExportEnabled).SetAttribute('value','false')
$config.Save($output+'.config')
& $output $work | Tee-Object -FilePath (Join-Path $work 'tests.log')
if($LASTEXITCODE -ne 0){throw 'Stock file tests failed.'}
