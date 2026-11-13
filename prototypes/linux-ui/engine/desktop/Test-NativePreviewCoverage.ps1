$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$out=Join-Path $root 'build/preview-coverage-check'
New-Item -ItemType Directory -Force $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=@(Get-ChildItem $PSScriptRoot -Filter '*.cs' | Where-Object Name -ne 'Program.cs' | ForEach-Object FullName)
$sources+=Join-Path $PSScriptRoot 'tests/NativePreviewCoverageTests.cs'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:NativePreviewCoverageTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$out/NativePreviewCoverage.tests.exe" @sources
if($LASTEXITCODE -ne 0){throw 'Coverage tests compilation failed.'}
& "$out/NativePreviewCoverage.tests.exe" $out
if($LASTEXITCODE -ne 0){throw 'Coverage tests failed.'}
