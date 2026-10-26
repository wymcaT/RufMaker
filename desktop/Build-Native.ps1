param([string]$Output)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $taskRoot 'build/RufMaker.native.exe'}
New-Item -ItemType Directory -Force (Split-Path -Parent $Output) | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$names=@('Program','AppWindow','AppPaths','Localization','EngineSetup','EngineSetupForm','EditorCore','CodeText','Motion','RandomParameters','ExpressionContext','ExpressionEditor','BulletFlight','RandomParameterUI','TimelineLayout','MotionPresets','BuiltinPresets','StylePresets','StyleEffects','PresetLibrary','PresetBrowser','BeatGrid','AudioAnalyzer','ProjectCommands','NewProjectForm','ProjectSettingsForm','NativeImportForm','NativeFilm','NativeGamePreview','NativeTestPlayer','Updater','Camera','WheelSafeControls','Rainbow','Trails','NativeAudio','PreviewPlayback','EditorCanvas','TimelineCanvas','PreviewRenderer','PreviewEffects','SelectionTimeControl','ParameterInspector','WorkspaceView','MainForm')
$names+=@('BulletSystem','BulletBehaviorForm','BulletTools','CurveControls','PlayerTrace','FormationShapes','TemplateRuntime','TemplateTools')
$names+=@('AudioSpectrogram','TimelineAudio','AudioTimelineTools','ObjectStatistics','StatisticsTools')
$names+=@('BulletLifetime')
$names+=@('SpawnAppearance','SpawnTimeline')
$names+=@('EditorShell','BehaviorWorkspace','GlobalVariables','GenerationPresets')
$names+=@('KeyframeWorkspace')
$names+=@('EventPresets')
$names+=@('CopyGeneration','TimelineEditing')
$sources=$names | ForEach-Object {Join-Path $PSScriptRoot ($_.ToString()+'.cs')}
& $compiler /nologo /target:winexe /platform:x86 /optimize+ /codepage:65001 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$(Join-Path $taskRoot 'assets/icons/icon.ico')" "/resource:$(Join-Path $taskRoot 'assets/icons/icon.ico'),RufMaker.App.ico" "/resource:$(Join-Path $taskRoot 'assets/locales/en.json'),RufMaker.en.json" "/out:$Output" @sources
if($LASTEXITCODE -ne 0){throw 'Native editor compilation failed.'}
Get-Item -LiteralPath $Output | Select-Object FullName,Length,LastWriteTime
