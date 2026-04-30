$stale = @(
    'KoreForge.AppLifecycle','KoreForge.Jex',
    'KoreForge.Metrics','KoreForge.Metrics.AspNet',
    'KoreForge.Scripts.Cli','KoreForge.Settings','KoreForge.Settings.Cli',
    'KoreForge.Templates','KoreForge.Time',
    'KoreForge.Web.HealthChecks','KoreForge.Web.RestApi.Observability'
)
foreach ($id in $stale) {
    $lower = $id.ToLowerInvariant()
    try {
        $j = Invoke-RestMethod "https://api.nuget.org/v3-flatcontainer/$lower/index.json" -TimeoutSec 20
        "$id => $($j.versions[-1])"
    } catch { "$id => ERROR" }
}
