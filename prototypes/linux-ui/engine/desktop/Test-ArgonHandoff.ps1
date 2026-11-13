$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot 'build/argon-handoff-check'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
& (Join-Path $taskRoot 'runtime/node.exe') (Join-Path $PSScriptRoot 'tests/verify-argon-handoff-gml.mjs') --prepare
if($LASTEXITCODE -ne 0){throw 'Argon handoff fixtures failed'}
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'ArgonHandoff.tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:TemplateSystemTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/TemplateSystemTests.cs')
if($LASTEXITCODE -ne 0){throw 'Argon handoff sampler compilation failed'}
& (Join-Path $taskRoot 'runtime/node.exe') (Join-Path $PSScriptRoot 'tests/verify-argon-handoff-gml.mjs') --preview
if($LASTEXITCODE -ne 0){throw 'Argon handoff preview checks failed'}
