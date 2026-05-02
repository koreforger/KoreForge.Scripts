using KoreForge.Scripts.Data;
using Microsoft.EntityFrameworkCore;

namespace KoreForge.Scripts.Tests;

internal static class TestDbFactory
{
    public static IDbContextFactory<KoreForgeScriptsDbContext> CreateInMemory(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<KoreForgeScriptsDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new InMemoryDbContextFactory(options);
    }

    private sealed class InMemoryDbContextFactory : IDbContextFactory<KoreForgeScriptsDbContext>
    {
        private readonly DbContextOptions<KoreForgeScriptsDbContext> _options;
        public InMemoryDbContextFactory(DbContextOptions<KoreForgeScriptsDbContext> options) => _options = options;
        public KoreForgeScriptsDbContext CreateDbContext() => new(_options);
    }
}
