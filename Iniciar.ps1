$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'DisparoApi.csproj') --launch-profile DisparoWhatsApp
