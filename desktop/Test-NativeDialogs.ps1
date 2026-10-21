$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $taskRoot 'build/NativeDialogs.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:NativeDialogTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/NativeDialogTests.cs')
if($LASTEXITCODE -ne 0){throw 'Dialog check compilation failed'}
& $output (Join-Path $taskRoot 'assets/icons/icon.ico')
if($LASTEXITCODE -ne 0){throw 'Dialog layout checks failed'}
