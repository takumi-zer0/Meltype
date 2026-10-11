[CmdletBinding()]
param([string]$Dotnet='dotnet',[string]$Python='python3',[ValidateSet('osx-arm64','osx-x64')][string]$Runtime='osx-arm64',[Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$LlamaArchive)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot
$package=[IO.Path]::GetFullPath($Output)
$contents=Join-Path $package 'ZeroVoice.app\Contents'
$app=Join-Path $contents 'MacOS'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
& $Dotnet publish (Join-Path $sourceRoot 'src\ZeroVoice.Desktop\ZeroVoice.Desktop.csproj') -c Release -r $Runtime --self-contained true -o $app --nologo
if($LASTEXITCODE -ne 0){throw 'Mac desktop build failed'}
& $Dotnet publish (Join-Path $sourceRoot 'src\ZeroVoice.Mac\ZeroVoice.Mac.csproj') -c Release -r $Runtime --self-contained true -o (Join-Path $app 'native') --nologo
if($LASTEXITCODE -ne 0){throw 'Mac helper build failed'}
$helper=Join-Path $app 'native\ZeroVoice.Mac'
(Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash | Set-Content -LiteralPath ($helper+'.sha256') -Encoding ascii
New-Item -ItemType Directory -Force -Path (Join-Path $app 'backend'),(Join-Path $app 'models') | Out-Null
& $Python (Join-Path $PSScriptRoot 'extract-runtime.py') $LlamaArchive (Join-Path $app 'backend')
if($LASTEXITCODE -ne 0){throw 'Runtime extraction failed'}
$llama=Get-ChildItem -LiteralPath (Join-Path $app 'backend') -Filter llama-completion -File -Recurse | Select-Object -First 1
if(-not $llama){throw 'llama-completion missing'}
$manifest=@(Get-ChildItem -LiteralPath (Join-Path $app 'backend') -File -Recurse | ForEach-Object {@{path=[IO.Path]::GetRelativePath($app,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
@{executable=[IO.Path]::GetRelativePath($app,$llama.FullName).Replace('\','/');model='models/Qwen_Qwen3-4B-Instruct-2507-Q4_K_M.gguf';modelSha256='2fde00ce69dd4899c70d020845e2638353015bba0fdf161b3eb965f2bca4464e';files=$manifest;gpuLayers=99} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $app 'model.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $sourceRoot 'native\macos\Info.plist') -Destination $contents
Copy-Item -LiteralPath (Join-Path $sourceRoot 'README-ja.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $sourceRoot 'VALIDATION-ja.md') -Destination $package
Copy-Item -LiteralPath (Join-Path $sourceRoot 'PERFORMANCE-ja.md') -Destination $package
foreach($file in @('Setup-Model.command','Connect-Handy.command','Disconnect-Handy.command')){Copy-Item -LiteralPath (Join-Path $sourceRoot ('scripts\'+$file)) -Destination $package}
foreach($notice in @('LICENSE','THIRD-PARTY-NOTICES.md','licenses')){Copy-Item -LiteralPath (Join-Path $sourceRoot $notice) -Destination $package -Recurse -Force}
Write-Output ('Built: '+$package)
