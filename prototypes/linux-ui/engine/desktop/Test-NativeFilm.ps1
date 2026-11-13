$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('native-film-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
try {
    & $compiler /nologo /target:exe /platform:x86 /codepage:65001 /reference:System.Drawing.dll ("/out:"+(Join-Path $scratch 'producer.exe')) (Join-Path $PSScriptRoot 'tests/FakeFilmProducer.cs')
    if($LASTEXITCODE -ne 0){throw 'Film producer fixture compilation failed'}
    $sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:NativeFilmTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+(Join-Path $buildRoot 'NativeFilm.tests.exe')) @sources (Join-Path $PSScriptRoot 'tests/NativeFilmTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Film check compilation failed'}
    & (Join-Path $buildRoot 'NativeFilm.tests.exe') $scratch
    if($LASTEXITCODE -ne 0){throw 'Film cache checks failed'}
} finally {
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('native-film-test-')){throw 'Unexpected test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
