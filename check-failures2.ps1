foreach ($r in @('KF.Jex.Cli','KF.Jex.LanguageServer')) {
    Write-Host "=== $r ===" -ForegroundColor Red
    $id = gh run list --repo "koreforger/$r" --limit 1 --json databaseId --jq '.[0].databaseId'
    gh run view $id --repo "koreforger/$r" --log-failed 2>&1 | Select-Object -First 30
    Write-Host ""
}

Write-Host "=== KoreForge.Templates log ===" -ForegroundColor Yellow
$id = gh run list --repo "koreforger/KoreForge.Templates" --limit 1 --json databaseId --jq '.[0].databaseId'
gh run view $id --repo "koreforger/KoreForge.Templates" --log 2>&1 | Select-String "Successfully created package|Pushing .*nupkg|already exists|Your package was pushed|dotnet pack"
