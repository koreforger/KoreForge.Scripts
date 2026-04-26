using KF.Scripts.Data;
using Microsoft.EntityFrameworkCore;

namespace KF.Scripts.Tests;

internal static class TestDbFactory
{
    public static IDbContextFactory<KFScriptsDbContext> CreateInMemory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<KFScriptsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new InMemoryDbContextFactory(options);
    }

    private sealed class InMemoryDbContextFactory : IDbContextFactory<KFScriptsDbContext>
    {
        private readonly DbContextOptions<KFScriptsDbContext> _options;
        public InMemoryDbContextFactory(DbContextOptions<KFScriptsDbContext> options) => _options = options;
        public KFScriptsDbContext CreateDbContext() => new(_options);
    }
}
