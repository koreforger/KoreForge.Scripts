using FluentAssertions;
using KoreForge.Scripts.Core.Services;
using KoreForge.Scripts.Data;
using KoreForge.Scripts.Data.Entities;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace KoreForge.Scripts.Tests;

public sealed class ScriptChangeMonitorTests
{
    private readonly ScriptStoreOptions _options = new()
    {
        ConnectionString = "unused",
        ApplicationId = "TestApp",
        PollingInterval = TimeSpan.FromMilliseconds(50)
    };

    [Fact]
    public async Task ShouldDetectAddedScript()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.CreateInMemory(dbName);
        var monitor = new ScriptChangeMonitor(factory, _options, NullLogger<ScriptChangeMonitor>.Instance);

        ScriptChangeEventArgs? eventArgs = null;
        monitor.ScriptsChanged += (_, args) => eventArgs = args;

        // Start monitor (initial load - empty)
        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Add a script
        await using (var db = factory.CreateDbContext())
        {
            db.Scripts.Add(new ScriptEntity
            {
                ApplicationId = "TestApp", Name = "new-one", TypeTag = "extract",
                Language = "test", Content = "body", CreatedBy = "u", CreatedDate = DateTime.UtcNow,
                ModifiedBy = "u", ModifiedDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Wait for poll
        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);

        eventArgs.Should().NotBeNull();
        eventArgs!.AddedScriptIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldDetectDeletedScript()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.CreateInMemory(dbName);

        // Seed a script
        await using (var db = factory.CreateDbContext())
        {
            db.Scripts.Add(new ScriptEntity
            {
                ApplicationId = "TestApp", Name = "to-delete", TypeTag = "extract",
                Language = "test", Content = "body", CreatedBy = "u", CreatedDate = DateTime.UtcNow,
                ModifiedBy = "u", ModifiedDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var monitor = new ScriptChangeMonitor(factory, _options, NullLogger<ScriptChangeMonitor>.Instance);
        ScriptChangeEventArgs? eventArgs = null;
        monitor.ScriptsChanged += (_, args) => eventArgs = args;

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Delete
        await using (var db = factory.CreateDbContext())
        {
            var entity = await db.Scripts.FirstAsync();
            db.Scripts.Remove(entity);
            await db.SaveChangesAsync();
        }

        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);

        eventArgs.Should().NotBeNull();
        eventArgs!.DeletedScriptIds.Should().HaveCount(1);
    }

    [Fact]
    public async Task NoChange_ShouldNotFireEvent()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.CreateInMemory(dbName);
        var monitor = new ScriptChangeMonitor(factory, _options, NullLogger<ScriptChangeMonitor>.Instance);

        var fired = false;
        monitor.ScriptsChanged += (_, _) => fired = true;

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(200);
        await monitor.StopAsync(CancellationToken.None);

        fired.Should().BeFalse();
    }
}
