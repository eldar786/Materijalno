param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\IdentifierQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent;$work=(Get-Content -LiteralPath (Join-Path $root 'artifacts/current_material_identifiers_run.txt')).Trim()
$exe=Join-Path $BuildDirectory 'IdentifierPresentationTests.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.ReportViewer.WinForms.dll','Microsoft.ReportViewer.Common.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$exe) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Windows.Forms.dll $refs (Join-Path $PSScriptRoot 'IdentifierPresentationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Presentation check compilation failed.'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Destination ($exe+'.config')
& $exe (Join-Path $work 'report_previews') | Tee-Object -FilePath (Join-Path $work 'presentation_tests.log')
if($LASTEXITCODE -ne 0){throw 'Presentation checks failed.'}
