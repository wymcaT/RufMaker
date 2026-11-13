$ErrorActionPreference = 'Stop'
$legacyDirectory = 'C:\Users\wum\AppData\Local\GameMaker8.2\extensions'
$installDirectory = 'D:\rufmaker\GameMaker8.2\extensions'
$backupDirectory = 'D:\rufmaker\build\extension-migration-20261019'
$extensionNames = @(
    'Derp XML 8.2.dat',
    'Derp XML 8.2.ged',
    'Game Maker 8.2 Live.dat',
    'Game Maker 8.2 Live.ged',
    'High Resolution Timer.chm',
    'High Resolution Timer.dat',
    'High Resolution Timer.ged'
)

New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
New-Item -ItemType Directory -Path "$backupDirectory\source" -Force | Out-Null
New-Item -ItemType Directory -Path "$backupDirectory\previous-install" -Force | Out-Null
$migrationRecords = @()

# Copy and verify every file before removing any source file.
foreach ($extensionName in $extensionNames) {
    $sourceFile = Join-Path $legacyDirectory $extensionName
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { continue }
    $installedFile = Join-Path $installDirectory $extensionName
    $backupFile = Join-Path "$backupDirectory\source" $extensionName
    $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
    if (Test-Path -LiteralPath $backupFile) {
        if ((Get-FileHash -LiteralPath $backupFile -Algorithm SHA256).Hash -ne $sourceHash) {
            throw "Existing backup differs: $backupFile"
        }
    } else {
        Copy-Item -LiteralPath $sourceFile -Destination $backupFile
    }
    if (Test-Path -LiteralPath $installedFile) {
        $installedHash = (Get-FileHash -LiteralPath $installedFile -Algorithm SHA256).Hash
        if ($installedHash -ne $sourceHash) {
            $previousBackup = Join-Path "$backupDirectory\previous-install" $extensionName
            if (Test-Path -LiteralPath $previousBackup) { throw "Previous install backup already exists: $previousBackup" }
            Copy-Item -LiteralPath $installedFile -Destination $previousBackup
        }
    }
    Copy-Item -LiteralPath $sourceFile -Destination $installedFile -Force
    foreach ($verifiedFile in @($installedFile, $backupFile)) {
        if ((Get-FileHash -LiteralPath $verifiedFile -Algorithm SHA256).Hash -ne $sourceHash) {
            throw "Copy verification failed: $verifiedFile"
        }
    }
    $migrationRecords += [pscustomobject]@{ Name=$extensionName; Source=$sourceFile; Destination=$installedFile; Backup=$backupFile; SHA256=$sourceHash }
}

if ($migrationRecords.Count -gt 0) {
    $migrationRecords | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$backupDirectory\manifest.json" -Encoding utf8
}
foreach ($migrationRecord in $migrationRecords) {
    if ((Get-FileHash -LiteralPath $migrationRecord.Source -Algorithm SHA256).Hash -ne $migrationRecord.SHA256) {
        throw "Source changed during migration: $($migrationRecord.Source)"
    }
    Remove-Item -LiteralPath $migrationRecord.Source
}
$migrationRecords | Select-Object Name,Destination,Backup | Format-List
Write-Output "Migrated and verified $($migrationRecords.Count) extension files."
