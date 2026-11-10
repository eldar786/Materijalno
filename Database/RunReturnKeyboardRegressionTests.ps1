param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\ReturnQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_return_keyboard_run.txt')).Trim()
$database=(Get-Content -LiteralPath (Join-Path $work 'database_name.txt')).Trim()
if($database -notmatch '^materijalno_fox_test_20261106_[0-9]{6}$'){throw 'Isolated return test database required.'}
$output=Join-Path $BuildDirectory 'ReturnKeyboardRegressionTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'ReturnKeyboardRegressionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Return check compilation failed.'}
$config=[xml](Get-Content -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Raw)
foreach($entry in @($config.configuration.connectionStrings.add)){
 if($entry.name -eq 'OracleFinansije'){[void]$entry.ParentNode.RemoveChild($entry);continue}
 $builder=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$entry.connectionString);$builder['Initial Catalog']=$database;$entry.SetAttribute('connectionString',$builder.ConnectionString)
}
($config.configuration.appSettings.add|Where-Object key -eq OracleExportEnabled).SetAttribute('value','false')
$config.Save($output+'.config')
if((Split-Path $BuildDirectory -Leaf) -eq 'ReturnQA'){$config.Save((Join-Path $BuildDirectory 'Materijalno.UI.exe.config'))}
& $output $database | Tee-Object -FilePath (Join-Path $work 'regression_tests.log')
if($LASTEXITCODE -ne 0){throw 'Return keyboard regression check failed.'}
