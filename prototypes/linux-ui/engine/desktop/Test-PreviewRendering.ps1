param([switch]$Baseline)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot 'build/tile-seam-fixture'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'PreviewRendering.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PreviewRenderingTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/PreviewRenderingTests.cs')
if($LASTEXITCODE -ne 0){throw 'Preview regression compilation failed'}
$image=Join-Path $taskRoot $(if($Baseline){'build/tile-seams-before.png'}else{'build/tile-seams-after.png'})
& $output $scratch $image $(if($Baseline){'baseline'}else{'verify'})
if($LASTEXITCODE -ne 0){throw 'Preview seam checks failed'}
