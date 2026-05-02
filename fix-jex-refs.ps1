$ErrorActionPreference = 'Stop'

$fixes = @(
    @{ Repo = 'KoreForge.Jex.Cli';          Tag = 'KoreForge.Jex.Cli/v0.0.11-alpha';          Branch = 'main' },
    @{ Repo = 'KoreForge.Jex.LanguageServer'; Tag = 'KoreForge.Jex.LanguageServer/v0.0.11-alpha'; Branch = 'main' }
)

foreach ($f in $fixes) {
    $path = "C:\My\KoreForge\$($f.Repo)"
    Write-Host "=== $($f.Repo) ===" -ForegroundColor Cyan
    Push-Location $path
    try {
        git add -A
        git commit -m "fix: replace cross-repo ProjectReference with PackageReference for KoreForge.Jex"
        git push origin $f.Branch
        # Delete old tag
        git tag -d $f.Tag 2>$null
        git push origin --delete $f.Tag 2>$null
        # Create and push new tag
        git tag -a $f.Tag -m "Release $($f.Tag)"
        git push origin $f.Tag
        Write-Host "  ✓ Committed, tagged $($f.Tag)" -ForegroundColor Green
    } catch {
        Write-Host "  ✗ Error: $_" -ForegroundColor Red
    } finally {
        Pop-Location
    }
}
