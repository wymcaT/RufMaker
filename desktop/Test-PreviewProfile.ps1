param([string]$Project,[int]$Start=550,[int]$Frames=60,[string[]]$Modes=@('cpu','gpu'),[switch]$VerifyOnly,[int]$PacedFrames=0)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$Project){$Project=Join-Path $root 'projects/rufmaker-demo.gm82'}
$out=Join-Path $root 'build/preview-gpu-check'
New-Item -Type Directory -Force $out | Out-Null
$data=Join-Path $out 'fixture.json'
& (Join-Path $root 'runtime/node.exe') (Join-Path $PSScriptRoot 'tests/preview-profile-fixture.mjs') $Project $data
if($LASTEXITCODE){throw 'Unable to read profile project'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object Name -ne Program.cs | Select-Object -ExpandProperty FullName
$exe=Join-Path $out 'PreviewProfile.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:PreviewProfile /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/out:"+$exe) @sources (Join-Path $PSScriptRoot 'tests/PreviewProfile.cs') (Join-Path $PSScriptRoot 'tests/PreviewProfileChecks.cs') (Join-Path $PSScriptRoot 'tests/PreviewPlaybackProfile.cs')
if($LASTEXITCODE){throw 'Profile build failed'}
$dll=Join-Path $root 'assets/native/x86/RufPreviewGpu.dll'
if(Test-Path $dll){$lib=New-Item -Type Directory -Force (Join-Path $out 'assets/native/x86');Copy-Item $dll $lib.FullName -Force}
if(!$VerifyOnly){
    foreach($fixture in 'original','stress'){
        foreach($mode in $Modes){
            & $exe $data $mode $fixture $Start $Frames
            if($LASTEXITCODE){throw 'Profile failed'}
        }
    }
}
if($Modes -contains 'cpu' -and $Modes -contains 'gpu'){
    & $exe $data compare both $Start $Frames
    if($LASTEXITCODE){throw 'Preview image comparison failed'}
    $cpuOnly=New-Item -Type Directory -Force (Join-Path $out 'cpu-only')
    Copy-Item -LiteralPath $exe -Destination $cpuOnly.FullName -Force
    Push-Location $cpuOnly.FullName
    try{
        & (Join-Path $cpuOnly.FullName 'PreviewProfile.exe') $data fallback original $Start 1
        if($LASTEXITCODE){throw 'Preview CPU fallback failed'}
    }finally{Pop-Location}
    & (Join-Path $root 'runtime/node.exe') (Join-Path $PSScriptRoot 'tests/preview-profile-report.mjs') $out
    if($LASTEXITCODE){throw 'Preview profile summary failed'}
}
if($PacedFrames -gt 0){
    & $exe $data paced stress $Start $PacedFrames
    if($LASTEXITCODE){throw 'Paced preview failed'}
}
