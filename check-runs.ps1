$repos = @('KF.Jex.Cli','KF.Jex.LanguageServer','KoreForge.AppLifecycle','KoreForge.Data','KoreForge.Jex','KoreForge.Json','KoreForge.Kafka','KoreForge.Logging','KoreForge.Logging.Serilog','KoreForge.Metrics','KoreForge.Metrics.AspNet','KoreForge.OData','KoreForge.Processing','KoreForge.Scripts','KoreForge.Settings','KoreForge.Templates','KoreForge.Time','KoreForge.Web')
foreach ($r in $repos) {
    $run = gh run list --repo "koreforger/$r" --limit 1 --json name,status,conclusion,headBranch,createdAt 2>$null | ConvertFrom-Json
    if ($run) {
        $s = $run[0].status
        $b = $run[0].headBranch
        $c = $run[0].conclusion
        Write-Host "$r => $s [$b] $c"
    } else {
        Write-Host "$r => no runs"
    }
}
