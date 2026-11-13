param([switch]$Online,[string]$Version)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$buildRoot=Join-Path $taskRoot 'build'
$scratch=Join-Path $buildRoot ('updater-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $sources=Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | Where-Object {$_.Name -ne 'Program.cs'} | Select-Object -ExpandProperty FullName
    $output=Join-Path $buildRoot 'Updater.tests.exe'
    & $compiler /nologo /target:exe /platform:x86 /optimize+ /codepage:65001 /main:UpdaterTests /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll ("/resource:"+(Join-Path $taskRoot 'assets/locales/en.json')+',RufMaker.en.json') ("/resource:"+(Join-Path $taskRoot 'assets/icons/icon.ico')+',RufMaker.App.ico') ("/out:"+$output) @sources (Join-Path $PSScriptRoot 'tests/UpdaterTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Updater checks compilation failed'}
    if($Online){
        if(!$Version){$Version=[regex]::Match((Get-Content (Join-Path $PSScriptRoot 'Updater.cs') -Raw),'Current="([0-9.]+)"').Groups[1].Value}
        & $output '--online' $scratch $Version
    }else{
        $stub=Join-Path $scratch 'RestartStub.exe'
        & $compiler /nologo /target:winexe /platform:x86 /codepage:65001 ("/out:"+$stub) (Join-Path $PSScriptRoot 'tests/UpdateRestartStub.cs')
        if($LASTEXITCODE -ne 0){throw 'Restart stub compilation failed'}
        & $output $scratch $stub
    }
    if($LASTEXITCODE -ne 0){throw 'Updater checks failed'}
}finally{
    $resolved=(Resolve-Path -LiteralPath $scratch).Path
    if((Split-Path -Parent $resolved) -ne (Resolve-Path -LiteralPath $buildRoot).Path -or !(Split-Path -Leaf $resolved).StartsWith('updater-test-')){throw 'Unexpected updater test cleanup path'}
    # The restart stub exits immediately after writing its marker.
    for($attempt=0;$attempt -lt 10;$attempt++){try{Remove-Item -LiteralPath $resolved -Recurse -Force;break}catch{if($attempt -eq 9){throw};Start-Sleep -Milliseconds 100}}
}
