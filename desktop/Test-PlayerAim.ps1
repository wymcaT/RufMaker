$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$scratch=Join-Path $taskRoot 'build/player-aim-check'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
$output=Join-Path $scratch 'PlayerAim.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PlayerAimTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/PlayerAimTests.cs')
if($LASTEXITCODE -ne 0){throw 'Player aim test compilation failed'}
& $output $scratch
if($LASTEXITCODE -ne 0){throw 'Player aim checks failed'}
