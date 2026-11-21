$ErrorActionPreference='Stop'
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$locator=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs=& $locator -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(!$vs){throw 'Install the Visual Studio C++ x86 build tools and Windows SDK to rebuild the optional preview DLL.'}
$vc=(Get-ChildItem -LiteralPath (Join-Path $vs 'VC/Tools/MSVC') -Directory | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1).FullName
$sdk=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$ver=(Get-ChildItem (Join-Path $sdk 'Include') -Directory | Where-Object {Test-Path (Join-Path $_.FullName 'um/d3d11.h')} | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1).Name
$scratch=New-Item -Type Directory -Force (Join-Path $root 'build/preview-gpu-native')
$dest=New-Item -Type Directory -Force (Join-Path $root 'assets/native/x86')
$fxc=Join-Path $sdk "bin/$ver/x86/fxc.exe"
foreach($entry in 'vs','screen','sprite','post','resample'){
    $target=if($entry -in 'vs','screen'){'vs_4_0'}else{'ps_4_0'}
    & $fxc /nologo /O3 /Qstrip_debug /Qstrip_reflect /T $target /E $entry /Vn "code_$entry" /Fh "$scratch/$entry.h" "$PSScriptRoot/Preview.hlsl"
    if($LASTEXITCODE){throw "Shader compilation failed for $entry"}
}
$artifact=Join-Path $scratch 'RufPreviewGpu'
$inc=@("/I$vc/include","/I$scratch") + (@('um','shared','winrt','ucrt')).ForEach({"/I$sdk/Include/$ver/$_"})
$libs=@("/LIBPATH:$vc/lib/x86","/LIBPATH:$sdk/Lib/$ver/um/x86","/LIBPATH:$sdk/Lib/$ver/ucrt/x86")
Push-Location $scratch.FullName
try{
    & "$vc/bin/Hostx64/x86/cl.exe" /nologo /EHsc /O2 /MT /LD /std:c++17 @inc "/Fo:$scratch/PreviewDevice.obj" "$PSScriptRoot/PreviewDevice.cpp" /link @libs d3d11.lib /NOIMPLIB "/OUT:$artifact.dll"
    if($LASTEXITCODE){throw 'Preview GPU DLL compilation failed'}
}finally{Pop-Location}
Copy-Item -LiteralPath "$artifact.dll" -Destination "$dest/RufPreviewGpu.dll" -Force
