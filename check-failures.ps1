foreach ($r in @('KoreForge.Jex.Cli','KoreForge.Jex.LanguageServer','KoreForge.Settings')) {
    Write-Host "=== $r ===" -ForegroundColor Red
    $id = gh run list --repo "koreforger/$r" --limit 1 --json databaseId --jq '.[0].databaseId'
    gh run view $id --repo "koreforger/$r" --log-failed 2>&1 | Select-Object -First 60
    Write-Host ""
}
