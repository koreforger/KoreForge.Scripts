[CmdletBinding()]
param(
    [string]$Configuration = 'Debug'
)

Push-Location (Resolve-Path "$PSScriptRoot\..")
try {
    dotnet build KoreForge.Scripts.sln --force -c $Configuration
    dotnet test  KoreForge.Scripts.sln -c $Configuration --no-build `
        --logger "html;LogFileName=TestResults.html" `
        --results-directory out/TestResults
    Write-Host 'Tests complete: out/TestResults/TestResults.html' -ForegroundColor Green
} finally {
    Pop-Location
}
