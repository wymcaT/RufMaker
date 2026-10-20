param([switch]$IconsOnly)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('preset-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
try {
    $sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
    $output=Join-Path $buildRoot 'Presets.tests.exe'
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PresetTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/out:$output" @sources (Join-Path $PSScriptRoot 'tests/PresetTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Preset checks compilation failed'}
    if($IconsOnly){& $output '--icons'}else{& $output $scratch (Join-Path $buildRoot 'verified-builtin-presets.json') (Join-Path $buildRoot 'verified-custom-presets.json')}
    if($LASTEXITCODE -ne 0){throw 'Preset checks failed'}
} finally {
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('preset-test-')){throw 'Unexpected preset test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
