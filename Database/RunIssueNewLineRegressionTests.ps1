param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\IssueQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_issue_run.txt')).Trim()
$output=Join-Path $BuildDirectory 'IssueNewLineRegressionTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'IssueNewLineRegressionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Regression test compilation failed.'}
Copy-Item -LiteralPath (Join-Path $root 'artifacts/FinancialQA/Materijalno.UI.exe.config') -Destination ($output+'.config')
& $output | Tee-Object -FilePath (Join-Path $work 'regression_tests.log')
if($LASTEXITCODE -ne 0){throw 'Issue new line regression check failed.'}
