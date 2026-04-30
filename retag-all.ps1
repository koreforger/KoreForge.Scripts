$root='C:\My\KoreForge'
$ver='v0.0.11-alpha'
$repos = @(
    'KF.Jex.Cli','KF.Jex.LanguageServer',
    'KoreForge.AppLifecycle','KoreForge.Data','KoreForge.Jex',
    'KoreForge.Json','KoreForge.Kafka','KoreForge.Logging','KoreForge.Logging.Serilog',
    'KoreForge.Metrics','KoreForge.Metrics.AspNet','KoreForge.OData','KoreForge.Processing',
    'KoreForge.Scripts','KoreForge.Settings','KoreForge.Templates','KoreForge.Time','KoreForge.Web'
)
foreach ($repo in $repos) {
    $path = "$root\$repo"
    $tag  = "$repo/$ver"
    Write-Host "=== $repo ===" -ForegroundColor Cyan
    $branch = git -C $path rev-parse --abbrev-ref HEAD 2>&1
    Write-Host "  branch: $branch"
    git -C $path tag -d $tag 2>$null | Out-Null
    git -C $path push origin ":refs/tags/$tag" 2>$null | Out-Null
    git -C $path tag -a $tag -m "Release $tag"
    $r = git -C $path push origin $tag 2>&1
    Write-Host "  $r"
}
Write-Host "Done."
