[CmdletBinding()]
param([string]$SettingsPath=(Join-Path $env:APPDATA 'com.pais.handy\settings_store.json'))
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'ZeroVoice.exe') --disconnect-handy $SettingsPath
if($LASTEXITCODE -ne 0){throw 'Restore failed. Exit Handy and check for settings changed since connection.'}
Write-Output 'The five changed settings were restored. Other settings were preserved.'
