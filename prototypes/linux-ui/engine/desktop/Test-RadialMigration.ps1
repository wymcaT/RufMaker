param([Parameter(Mandatory=$true)][string]$Baseline,[Parameter(Mandatory=$true)][string]$Candidate,[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'build/radial-migration-check'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
# Mirror the exact product source list rather than collecting arbitrary test helpers.
$build=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Build-Native.ps1') -Raw
$names=@();foreach($block in [regex]::Matches($build,'\$names\s*\+?=\s*@\(([^)]*)\)')){foreach($name in [regex]::Matches($block.Groups[1].Value,"'([^']+)'")){$names+=$name.Groups[1].Value}}
$sources=$names | Where-Object {$_ -ne 'Program'} | ForEach-Object {Join-Path $PSScriptRoot ($_+'.cs')}
$output=Join-Path $OutputDirectory 'RadialMigration.tests.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:RadialMigrationTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/RadialMigrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Radial migration tests compilation failed'}
& $output $Baseline $Candidate (Join-Path $OutputDirectory 'migration-result.json')
if($LASTEXITCODE -ne 0){throw 'Radial migration tests failed'}
