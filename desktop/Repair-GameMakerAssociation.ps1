param([string]$GameMakerPath)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
if(!$GameMakerPath){$GameMakerPath=Join-Path $taskRoot 'GameMaker8.2\GameMaker.exe'}
$GameMakerPath=(Resolve-Path -LiteralPath $GameMakerPath).Path
$installDirectory=Split-Path -Parent $GameMakerPath
foreach($extension in @('Game Maker 8.2 Core','Game Maker 8.2 Sound')){
    foreach($suffix in @('.ged','.dat')){
        $extensionFile=Join-Path $installDirectory ('extensions\'+$extension+$suffix)
        if(!(Test-Path -LiteralPath $extensionFile -PathType Leaf)){
            throw "Missing extension: $extensionFile. Choose the complete GameMaker 8.2 installation."
        }
    }
}

$backupDirectory=Join-Path $taskRoot ('build\gamemaker-association-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
foreach($className in @('.gm82','gm82file')){
    $registryPath='HKCU\Software\Classes\'+$className
    if(Test-Path -LiteralPath ('Registry::HKEY_CURRENT_USER\Software\Classes\'+$className)){
        & reg.exe export $registryPath (Join-Path $backupDirectory ($className+'.reg')) /y | Out-Null
        if($LASTEXITCODE -ne 0){throw "Could not back up $registryPath"}
    }
}

# Fix only the current user's file association; keep both installations intact.
$projectKey=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes\.gm82')
try{$projectKey.SetValue('','gm82file')}finally{$projectKey.Dispose()}
$commandKey=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes\gm82file\shell\open\command')
$openCommand='"'+$GameMakerPath+'" "%1"'
try{$commandKey.SetValue('',$openCommand)}finally{$commandKey.Dispose()}
$iconKey=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes\gm82file\DefaultIcon')
try{$iconKey.SetValue('','"'+$GameMakerPath+'",0')}finally{$iconKey.Dispose()}

if(!('RufMaker.FileAssociationNotification' -as [type])){
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace RufMaker {
    public static class FileAssociationNotification {
        [DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    }
}
'@
}
[RufMaker.FileAssociationNotification]::SHChangeNotify(0x08000000,0,[IntPtr]::Zero,[IntPtr]::Zero)
[pscustomobject]@{OpenCommand=$openCommand;BackupDirectory=$backupDirectory}
