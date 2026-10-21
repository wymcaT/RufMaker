$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('preview-playback-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try{
    $wave=Join-Path $scratch 'silence.wav'
    $stream=[IO.File]::Create($wave);$writer=[IO.BinaryWriter]::new($stream)
    try{$dataSize=44100*2*6;$writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'));$writer.Write([int](36+$dataSize));$writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$writer.Write([int]16);$writer.Write([short]1);$writer.Write([short]1);$writer.Write([int]44100);$writer.Write([int]88200);$writer.Write([short]2);$writer.Write([short]16);$writer.Write([Text.Encoding]::ASCII.GetBytes('data'));$writer.Write([int]$dataSize);$writer.Write([byte[]]::new($dataSize))}finally{$writer.Dispose();$stream.Dispose()}
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
    $output=Join-Path $buildRoot 'PreviewPlayback.tests.exe'
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PreviewPlaybackTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/PreviewPlaybackTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Preview playback check compilation failed'}
    Push-Location $taskRoot
    try{& $output $scratch $wave;if($LASTEXITCODE -ne 0){throw 'Preview playback checks failed'}}finally{Pop-Location}
}finally{
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('preview-playback-')){throw 'Unexpected playback cleanup path'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
