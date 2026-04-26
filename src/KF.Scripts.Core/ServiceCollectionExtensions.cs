using KF.Scripts.Core.Services;
using KF.Scripts.Data;
using KF.Scripts.Interfaces;
using KF.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KF.Scripts.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKoreForgeScripts(this IServiceCollection services, Action<ScriptStoreOptions> configure)
    {
        var opts = new ScriptStoreOptions();
        configure(opts);

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
            throw new InvalidOperationException("ScriptStoreOptions.ConnectionString is required.");

        services.TryAddSingleton(opts);
        services.AddDbContextFactory<KFScriptsDbContext>(o => o.UseSqlServer(opts.ConnectionString));
        services.TryAddScoped<IScriptStore, ScriptStore>();
        services.TryAddScoped<IScriptHistoryService, ScriptHistoryService>();
        services.TryAddSingleton<IScriptValidator, ScriptValidator>();

        var monitor = new ScriptChangeMonitor(
            services.BuildServiceProvider().GetRequiredService<IDbContextFactory<KFScriptsDbContext>>(),
            opts,
            services.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScriptChangeMonitor>>());

        // Register as both IScriptChangeNotification and IHostedService
        services.TryAddSingleton<ScriptChangeMonitor>();
        services.TryAddSingleton<IScriptChangeNotification>(sp => sp.GetRequiredService<ScriptChangeMonitor>());
        services.AddHostedService(sp => sp.GetRequiredService<ScriptChangeMonitor>());

        return services;
    }

    public static IServiceCollection AddScriptCompiler<T>(this IServiceCollection services, string language)
        where T : class, IScriptCompiler
    {
        services.AddSingleton<IScriptCompiler, T>();
        return services;
    }
}
