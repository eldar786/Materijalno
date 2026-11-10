param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\ReceiptQA',[string]$SourceFolder='C:\Users\eldar.bajrovic\OneDrive - Lutrija BiH\Radna površina\MATVM')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_receipt_print_run.txt')).Trim()
$output=Join-Path $BuildDirectory 'ReceiptPrintTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Data.DataSetExtensions.dll /reference:System.Xml.dll /reference:System.Windows.Forms.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'ReceiptPrintTests.cs') (Join-Path $PSScriptRoot 'DbfReader.cs')
if($LASTEXITCODE -ne 0){throw 'Receipt check compilation failed.'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Destination ($output+'.config')
& $output $SourceFolder (Join-Path $work 'reports') | Tee-Object -FilePath (Join-Path $work 'receipt_tests.log')
if($LASTEXITCODE -ne 0){throw 'Receipt report checks failed.'}
