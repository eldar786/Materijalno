param([string]$BuildDirectory=(Join-Path $PSScriptRoot '..\artifacts\FoxApp'))
$ErrorActionPreference='Stop'
$output=Join-Path $BuildDirectory 'TransferViewBindingTests.exe'
$compiler='C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\Roslyn\csc.exe'
$refs=@('Materijalno.UI.exe','Materijalno.Model.dll','Materijalno.ViewModel.dll') | ForEach-Object {'/reference:'+(Join-Path $BuildDirectory $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll','System.Xaml.dll') | ForEach-Object {'/reference:C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\'+$_}
& $compiler /nologo /target:exe /platform:x86 /langversion:latest ('/out:'+$output) '/reference:System.Core.dll' $refs (Join-Path $PSScriptRoot 'TransferViewBindingTests.cs')
if($LASTEXITCODE -ne 0){throw 'WPF test compilation failed.'}
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'Materijalno.UI.exe.config') -Destination ($output+'.config')
& $output
if($LASTEXITCODE -ne 0){throw 'WPF binding regression test failed.'}
