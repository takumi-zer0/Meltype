[CmdletBinding()]
param([string]$SettingsPath=(Join-Path $env:APPDATA 'com.pais.handy\settings_store.json'))
$ErrorActionPreference='Stop'
$executable=Join-Path $PSScriptRoot 'ZeroVoice.exe'
if(-not(Test-Path -LiteralPath $executable)){throw 'Run this script from the built ZeroVoice folder.'}
if(Get-Process -Name Handy -ErrorAction SilentlyContinue){throw 'Exit Handy from its tray icon first.'}
& $executable --connect-handy $SettingsPath
if($LASTEXITCODE -ne 0){throw 'Settings update failed. Existing settings were not deliberately overwritten after a conflict. See the application error output.'}
Write-Output 'Connected. Start Handy and use its ordinary transcription shortcut. ZeroVoice will show the recognition result for review.'
