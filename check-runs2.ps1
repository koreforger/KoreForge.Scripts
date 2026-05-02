$repos = @(
    'KoreForge.Jex.Cli','KoreForge.Jex.LanguageServer',
    'KoreForge.AppLifecycle','KoreForge.Jex','KoreForge.Metrics','KoreForge.Metrics.AspNet',
    'KoreForge.Scripts','KoreForge.Settings','KoreForge.Templates','KoreForge.Time','KoreForge.Web'
)
foreach ($r in $repos) {
    $run = gh run list --repo "koreforger/$r" --limit 1 --json name,status,conclusion,headBranch,createdAt 2>$null | ConvertFrom-Json
    if ($run) {
        "$r => $($run[0].status) [$($run[0].headBranch)] $($run[0].conclusion)"
    } else { "$r => no runs" }
}
