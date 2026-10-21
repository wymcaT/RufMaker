param([string]$Version,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$Version){$Version=[regex]::Match((Get-Content (Join-Path $PSScriptRoot 'Updater.cs') -Raw),'Current="([0-9.]+)"').Groups[1].Value}
if($Version -notmatch '^\d+\.\d+\.\d+$'){throw 'Use a version such as 1.1.0.'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $taskRoot 'build/distribution'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$executable=Join-Path $taskRoot 'build/RufMaker.release.exe'
& (Join-Path $PSScriptRoot 'Build-Native.ps1') -Output $executable | Out-Null
if([Diagnostics.FileVersionInfo]::GetVersionInfo($executable).ProductVersion -ne $Version){throw 'Tag and application versions do not match.'}
Add-Type -AssemblyName System.IO.Compression
$zipPath=Join-Path $OutputDirectory "RufMaker-$Version.zip"
if(Test-Path -LiteralPath $zipPath){throw "Package already exists: $zipPath"}
$manifestFiles=New-Object 'System.Collections.Generic.List[object]'
$packageFiles=New-Object 'System.Collections.Generic.List[object]'
$addFile={param($relative,$source)
    $packageFiles.Add(@{path=$relative;source=$source})
    $manifestFiles.Add(@{path=$relative;sha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()})
}
& $addFile 'RufMaker.exe' $executable
foreach($relative in @('README.md','LICENSE','runtime/node.exe','runtime/fmodex.dll')){& $addFile $relative (Join-Path $taskRoot $relative)}
# Keep transport paths compatible with the 1.6 updater. The new EXE arranges
# these shipped assets into assets/ before loading a project.
foreach($entry in @(@('icon.ico','assets/icons/icon.ico'),@('desktop/assets/sprRufBlock.png','assets/terrain/sprRufBlock.png'),@('desktop/assets/sprRufPlatform.png','assets/terrain/sprRufPlatform.png'))){& $addFile $entry[0] (Join-Path $taskRoot $entry[1])}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'studio') -File | Where-Object {$_.Extension -eq '.mjs' -or $_.Name -eq 'waveform.json'}){& $addFile ('studio/'+$file.Name) $file.FullName}
foreach($folder in @('templates/avoidance.gm82','assets/sprite-packs/Ultimate Avoidance Sprite Pack')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $taskRoot $folder) -File -Recurse){
        $relative=$file.FullName.Substring($taskRoot.Length+1).Replace('\','/')
        if($relative.StartsWith('assets/sprite-packs/')){$relative=$relative.Substring('assets/sprite-packs/'.Length)}
        if($relative -match '/(cache|build)/|/Data/Save/(?!\.gitkeep$)|rufmaker_preview|game_errors\.log|/options\.ini$|\.tmp$'){continue}
        & $addFile $relative $file.FullName
    }
}
$manifest=@{version=$Version;files=$manifestFiles.ToArray()} | ConvertTo-Json -Depth 5
$zip=[System.IO.Compression.ZipArchive]::new([System.IO.File]::Create($zipPath),[System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($item in $packageFiles){$entry=$zip.CreateEntry($item.path,[System.IO.Compression.CompressionLevel]::Optimal);$inputStream=[System.IO.File]::OpenRead($item.source);$outputStream=$entry.Open();try{$inputStream.CopyTo($outputStream)}finally{$outputStream.Dispose();$inputStream.Dispose()}}
    $entry=$zip.CreateEntry('update-manifest.json');$stream=$entry.Open();$writer=[System.IO.StreamWriter]::new($stream,[System.Text.UTF8Encoding]::new($false));try{$writer.Write($manifest)}finally{$writer.Dispose()}
}finally{$zip.Dispose()}
$hash=(Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($zipPath+'.sha256',"$hash  $([System.IO.Path]::GetFileName($zipPath))`n",[System.Text.Encoding]::ASCII)
Get-Item -LiteralPath $zipPath,($zipPath+'.sha256') | Select-Object FullName,Length
