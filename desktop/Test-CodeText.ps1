$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$output=Join-Path $taskRoot 'build/CodeText.tests.exe'
New-Item -ItemType Directory -Force (Split-Path -Parent $output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /codepage:65001 "/out:$output" (Join-Path $PSScriptRoot 'CodeText.cs') (Join-Path $PSScriptRoot 'tests/CodeTextTests.cs')
if($LASTEXITCODE -ne 0){throw 'Code text check compilation failed'}
& $output (Join-Path $taskRoot 'projects/imported/wel-导入/GameMaker.gm82/objects/obj_wymad.gml')
if($LASTEXITCODE -ne 0){throw 'Code text checks failed'}
