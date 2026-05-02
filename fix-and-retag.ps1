$root='C:\My\KoreForge'
$ver='v0.0.11-alpha'
$repos = @('KoreForge.Jex.Cli','KoreForge.Jex.LanguageServer','KoreForge.Settings')
foreach ($repo in $repos) {
    $path = "$root\$repo"
    $tag  = "$repo/$ver"
    Write-Host "=== $repo ===" -ForegroundColor Cyan
    git -C $path add .github/workflows/publish-nuget.yml
    $msg = git -C $path commit -m "ci: fix publish workflow" 2>&1
    Write-Host "  commit: $msg"
    git -C $path push origin (git -C $path rev-parse --abbrev-ref HEAD) 2>&1 | Out-Null
    git -C $path tag -d $tag 2>$null | Out-Null
    git -C $path push origin ":refs/tags/$tag" 2>$null | Out-Null
    git -C $path tag -a $tag -m "Release $tag"
    $r = git -C $path push origin $tag 2>&1
    Write-Host "  tag push: $r"
}
Write-Host "Done."
