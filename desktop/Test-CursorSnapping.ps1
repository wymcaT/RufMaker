$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot 'build/cursor-snap-check'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'CursorSnapping.tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:CursorSnappingTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/CursorSnappingTests.cs') (Join-Path $PSScriptRoot 'tests/TimelineCursorRenderingTests.cs')
if($LASTEXITCODE -ne 0){throw 'Cursor snapping test compilation failed'}
& $output $scratch
if($LASTEXITCODE -ne 0){throw 'Cursor snapping checks failed'}
