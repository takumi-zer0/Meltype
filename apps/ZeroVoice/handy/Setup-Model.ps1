param([string]$ExistingModel)
$ErrorActionPreference='Stop'
$modelDirectory=Join-Path $env:LOCALAPPDATA 'Handy-ZeroVoice\models'
$modelPath=Join-Path $modelDirectory 'gemma-4-E2B_q4_0-it.gguf'
$expected='fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634'
New-Item -ItemType Directory -Force -Path $modelDirectory | Out-Null
if((Test-Path -LiteralPath $modelPath) -and (Get-FileHash -LiteralPath $modelPath -Algorithm SHA256).Hash -eq $expected){
    Write-Output 'Gemma 4 model is already verified.'
    exit 0
}
$temporary=Join-Path $modelDirectory ([Guid]::NewGuid().ToString('N')+'.download')
try {
    if($ExistingModel){
        $source=(Resolve-Path -LiteralPath $ExistingModel).Path
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected){throw 'Source model checksum mismatch'}
        Copy-Item -LiteralPath $source -Destination $temporary
    }else{
        [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri 'https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/resolve/675cff42a74c774d6cb76f76d8eacb49b48c9b93/gemma-4-E2B_q4_0-it.gguf' -OutFile $temporary
    }
    if((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expected){throw 'Downloaded model checksum mismatch'}
    Move-Item -LiteralPath $temporary -Destination $modelPath -Force
    Write-Output 'Gemma 4 model is verified. Start handy.exe after exiting any other Handy instance.'
}finally{
    if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary}
}
