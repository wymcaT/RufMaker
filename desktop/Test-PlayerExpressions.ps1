$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot ('build/player-expressions-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
& (Join-Path $taskRoot 'runtime/node.exe') (Join-Path $PSScriptRoot 'tests/expression-fixtures.mjs') (Join-Path $scratch 'cases.json')
if($LASTEXITCODE -ne 0){throw 'Expression fixture generation failed'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'PlayerExpressions.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PlayerExpressionTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/PlayerExpressionTests.cs')
if($LASTEXITCODE -ne 0){throw 'Expression test compilation failed'}
& $output (Join-Path $scratch 'cases.json') $scratch (Join-Path $taskRoot 'build')
if($LASTEXITCODE -ne 0){throw 'Player expression checks failed'}
