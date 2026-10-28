$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $root 'build/timeline-group-check'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'TimelineGroups.tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:TimelineGroupTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $root 'assets/locales/en.json')+',RufMaker.en.json') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/TimelineGroupTests.cs')
if($LASTEXITCODE -ne 0){throw 'Group tests compilation failed'}
& $output $scratch
if($LASTEXITCODE -ne 0){throw 'Group tests failed'}
