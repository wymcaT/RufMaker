$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build-Native.ps1') -Output (Join-Path $taskRoot 'RufMaker.Beta.exe')
