$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$folder=Join-Path $taskRoot 'build/audio-statistics-check'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $folder 'AudioStatistics.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:AudioStatisticsTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/AudioStatisticsTests.cs')
if($LASTEXITCODE -ne 0){throw 'Audio/statistics test compilation failed'}
& $output $taskRoot $folder
if($LASTEXITCODE -ne 0){throw 'Audio/statistics checks failed'}
