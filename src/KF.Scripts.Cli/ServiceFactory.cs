using KF.Scripts.Core;
using KF.Scripts.Data;
using KF.Scripts.Interfaces;
using KF.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KF.Scripts.Cli;

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
        services.AddDbContextFactory<KFScriptsDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IScriptStore, KF.Scripts.Core.Services.ScriptStore>();
        services.AddScoped<IScriptHistoryService, KF.Scripts.Core.Services.ScriptHistoryService>();
        services.AddSingleton<IScriptValidator, KF.Scripts.Core.Services.ScriptValidator>();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        return services.BuildServiceProvider();
    }
}
