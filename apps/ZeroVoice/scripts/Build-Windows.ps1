[CmdletBinding()]
param([string]$Dotnet='dotnet',[Parameter(Mandatory)][string]$Output,[Parameter(Mandatory)][string]$LlamaDirectory,[Parameter(Mandatory)][string]$ModelFile)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path $PSScriptRoot
$outputRoot=[IO.Path]::GetFullPath($Output)
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
& $Dotnet publish (Join-Path $sourceRoot 'src\ZeroVoice.Desktop\ZeroVoice.Desktop.csproj') -c Release -r win-x64 --self-contained true -o $outputRoot --nologo
if($LASTEXITCODE -ne 0){throw 'Desktop build failed'}
& $Dotnet publish (Join-Path $sourceRoot 'src\ZeroVoice.Windows\ZeroVoice.Windows.csproj') -c Release -r win-x64 --self-contained true -o (Join-Path $outputRoot 'native') --nologo
if($LASTEXITCODE -ne 0){throw 'Native helper build failed'}
$helper=Join-Path $outputRoot 'native\ZeroVoice.Windows.exe'
(Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash | Set-Content -LiteralPath ($helper+'.sha256') -Encoding ascii
New-Item -ItemType Directory -Force -Path (Join-Path $outputRoot 'backend'),(Join-Path $outputRoot 'models') | Out-Null
$llamaSource=[IO.Path]::GetFullPath($LlamaDirectory)
$runtimeFiles=@(Get-ChildItem -LiteralPath $llamaSource -File | Where-Object {$_.Extension -eq '.dll' -or $_.Name -eq 'llama-completion.exe'})
$manifest=@()
foreach($file in $runtimeFiles){
  $destination=Join-Path $outputRoot ('backend\'+$file.Name)
  Copy-Item -LiteralPath $file.FullName -Destination $destination
  $manifest+=@{path=('backend/'+$file.Name);sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}
}
$modelName=Split-Path $ModelFile -Leaf
$modelDestination=Join-Path $outputRoot ('models\'+$modelName)
if(-not(Test-Path -LiteralPath $modelDestination)){Copy-Item -LiteralPath $ModelFile -Destination $modelDestination}
$modelHash=(Get-FileHash -LiteralPath $modelDestination -Algorithm SHA256).Hash
if($modelHash -ine '2fde00ce69dd4899c70d020845e2638353015bba0fdf161b3eb965f2bca4464e'){throw 'Unexpected model hash'}
@{executable='backend/llama-completion.exe';model=('models/'+$modelName);modelSha256=$modelHash;files=$manifest;gpuLayers=99} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'model.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $sourceRoot 'README-ja.md') -Destination (Join-Path $outputRoot 'README-ja.md')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'VALIDATION-ja.md') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'PERFORMANCE-ja.md') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts\Connect-Handy.ps1') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $sourceRoot 'scripts\Disconnect-Handy.ps1') -Destination $outputRoot
foreach($notice in @('LICENSE','THIRD-PARTY-NOTICES.md','licenses')){Copy-Item -LiteralPath (Join-Path $sourceRoot $notice) -Destination $outputRoot -Recurse -Force}
& (Join-Path $outputRoot 'ZeroVoice.exe') --prepare-cache | Out-Host
if($LASTEXITCODE -ne 0){throw 'Fixed instruction preparation failed'}
Write-Output ('Built: '+$outputRoot)
