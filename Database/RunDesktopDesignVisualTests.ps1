param([string]$BuildDirectory='D:\Materijalno Novo\artifacts\DesignQA')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$designRun=Get-Content -LiteralPath (Join-Path $root 'artifacts/current_design_run.txt')
$qaConfig=Join-Path $root 'artifacts/FinancialQA/Materijalno.UI.exe.config'
Copy-Item -LiteralPath $qaConfig -Destination (Join-Path $BuildDirectory 'DesktopDesignVisualTests.exe.config')
if((Split-Path $BuildDirectory -Leaf) -eq 'DesignQA'){Copy-Item -LiteralPath $qaConfig -Destination (Join-Path $BuildDirectory 'Materijalno.UI.exe.config')}
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll','Microsoft.EntityFrameworkCore.dll','Microsoft.EntityFrameworkCore.Relational.dll','Microsoft.Bcl.AsyncInterfaces.dll')|ForEach-Object{'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll')|ForEach-Object{'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
$output=Join-Path $BuildDirectory 'DesktopDesignVisualTests.exe'
& 'C:/Program Files/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/Roslyn/csc.exe' /nologo /platform:x86 /langversion:latest ('/out:'+$output) /reference:System.Core.dll /reference:System.Data.dll /reference:System.Configuration.dll '/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\Facades\netstandard.dll' $refs (Join-Path $PSScriptRoot 'DesktopDesignVisualTests.cs')
if($LASTEXITCODE -ne 0){throw 'Visual check compilation failed.'}
& $output (Join-Path $designRun 'renders') | Tee-Object -FilePath (Join-Path $designRun 'visual_tests.log')
if($LASTEXITCODE -ne 0){throw 'Visual check failed.'}
