using KoreForge.Scripts.Core;
using KoreForge.Scripts.Data;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KoreForge.Scripts.Cli;

internal static class ServiceFactory
{
    public static ServiceProvider Build(string connectionString, string applicationId)
    {
        var services = new ServiceCollection();
        var opts = new ScriptStoreOptions
        {
            ConnectionString = connectionString,
            ApplicationId = applicationId
        };

        services.AddSingleton(opts);
        services.AddDbContextFactory<KoreForgeScriptsDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IScriptStore, KoreForge.Scripts.Core.Services.ScriptStore>();
        services.AddScoped<IScriptHistoryService, KoreForge.Scripts.Core.Services.ScriptHistoryService>();
        services.AddSingleton<IScriptValidator, KoreForge.Scripts.Core.Services.ScriptValidator>();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        return services.BuildServiceProvider();
    }
}
