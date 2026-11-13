$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('ui-save-safety-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
    $output=Join-Path $scratch 'UiSaveSafety.tests.exe'
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:UiSaveSafetyTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/UiSaveSafetyTests.cs')
    if($LASTEXITCODE -ne 0){throw 'UI save safety test compilation failed'}
    & $output $scratch
    if($LASTEXITCODE -ne 0){throw 'UI save safety checks failed'}
} finally {
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('ui-save-safety-')){throw 'Unexpected test cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
