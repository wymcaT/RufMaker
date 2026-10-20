$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('native-replay-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
try {
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 ("/out:"+(Join-Path $scratch 'fake-native.exe')) (Join-Path $PSScriptRoot 'tests/FakeNativeRunner.cs')
    if($LASTEXITCODE -ne 0){throw 'Fake runner compilation failed'}
    & $compiler /nologo /nowarn:0649 /target:exe /platform:x86 /optimize+ /codepage:65001 ("/out:"+(Join-Path $scratch 'replay-test.exe')) (Join-Path $PSScriptRoot 'NativePreviewRunner.cs') (Join-Path $PSScriptRoot 'tests/NativeReplayRunnerTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Replay check compilation failed'}
    & (Join-Path $scratch 'replay-test.exe') $scratch
    if($LASTEXITCODE -ne 0){throw 'Replay process checks failed'}
} finally {
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('native-replay-test-')){throw 'Unexpected test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
