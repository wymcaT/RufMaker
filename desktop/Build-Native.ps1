param([string]$Output)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $taskRoot 'build/RufMaker.native.exe'}
New-Item -ItemType Directory -Force (Split-Path -Parent $Output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$names=@('Program','EditorCore','CodeText','Motion','MotionPresets','BeatGrid','AudioAnalyzer','ProjectCommands','NewProjectForm','ProjectSettingsForm','NativeImportForm','NativeFilm','NativeGamePreview','Updater','Camera','WheelSafeControls','Rainbow','Trails','NativeAudio','EditorCanvas','TimelineCanvas','PreviewRenderer','PreviewEffects','SelectionTimeControl','ParameterInspector','WorkspaceView','MainForm')
$sources=$names | ForEach-Object {Join-Path $PSScriptRoot ($_.ToString()+'.cs')}
& $compiler /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$(Join-Path $taskRoot 'icon.ico')" "/out:$Output" @sources
if($LASTEXITCODE -ne 0){throw 'Native editor compilation failed.'}
Get-Item -LiteralPath $Output | Select-Object FullName,Length,LastWriteTime
