[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

Push-Location (Resolve-Path "$PSScriptRoot\..")
try {
    dotnet build KoreForge.Scripts.slnx --force -c $Configuration
    Write-Host 'Build complete.' -ForegroundColor Green
} finally {
    Pop-Location
}
