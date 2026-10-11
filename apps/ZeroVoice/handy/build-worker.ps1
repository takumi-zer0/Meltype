param([Parameter(Mandatory)][string]$Runtime,[Parameter(Mandatory)][string]$Output)
$ErrorActionPreference='Stop'
$source=Split-Path $PSScriptRoot
$dest=[IO.Path]::GetFullPath($Output)
dotnet publish "$source/src/ZeroVoice.Worker" -c Release -r $Runtime --self-contained true -o $dest
if($LASTEXITCODE){throw 'Worker build failed'}
$native=if($Runtime -eq 'win-x64'){'ZeroVoice.Windows'}else{'ZeroVoice.Mac'}
dotnet publish "$source/src/$native" -c Release -r $Runtime --self-contained true -o "$dest/native"
if($LASTEXITCODE){throw 'Native helper build failed'}
foreach($dir in @($dest,"$dest/native")){
  $files=@(Get-ChildItem -LiteralPath $dir -File | Where-Object {$_.Name -ne 'files.json'} | ForEach-Object {@{path=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
  ConvertTo-Json -InputObject $files -Depth 4 | Set-Content "$dir/files.json" -Encoding utf8NoBOM
}
