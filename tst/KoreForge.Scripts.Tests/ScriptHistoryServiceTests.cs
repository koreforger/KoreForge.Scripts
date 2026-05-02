using FluentAssertions;
using KoreForge.Scripts.Core.Services;
using KoreForge.Scripts.Data;
using KoreForge.Scripts.Data.Entities;
using KoreForge.Scripts.Exceptions;
using KoreForge.Scripts.Models;
using KoreForge.Scripts.Options;
using Microsoft.EntityFrameworkCore;
using KoreForge.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace KoreForge.Scripts.Tests;

public sealed class ScriptHistoryServiceTests
{
    private readonly ScriptStoreOptions _options = new()
    {
        ConnectionString = "unused",
        ApplicationId = "TestApp"
    };

    private (ScriptStore store, ScriptHistoryService history, string dbName) CreateServices()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = TestDbFactory.CreateInMemory(dbName);
        var compilers = new[] { new PassThroughCompiler() };
        var store = new ScriptStore(factory, _options, compilers, NullLogger<ScriptStore>.Instance, UtcSystemClock.Instance);
        var history = new ScriptHistoryService(factory, store, _options, NullLogger<ScriptHistoryService>.Instance);
        return (store, history, dbName);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnOrderedHistory()
    {
        var (store, history, _) = CreateServices();
        var created = await store.CreateAsync(new CreateScriptRequest("hist1", "extract", "test", "v1", null, "u", null));
        await store.UpdateAsync(new UpdateScriptRequest(created.ScriptId, "v2", null, null, created.RowVersion, "u", "update1"));

        var records = await history.GetHistoryAsync("hist1");

        records.Should().HaveCount(2);
        records[0].Operation.Should().Be(ScriptOperation.Update); // most recent first
        records[1].Operation.Should().Be(ScriptOperation.Insert);
    }

    [Fact]
    public async Task RollbackAsync_ShouldRestoreContent()
    {
        var (store, history, _) = CreateServices();
        var created = await store.CreateAsync(new CreateScriptRequest("rb1", "extract", "test", "original", null, "u", null));
        var updated = await store.UpdateAsync(new UpdateScriptRequest(created.ScriptId, "modified", null, null, created.RowVersion, "u", null));

        // History: [0] = Update (most recent), [1] = Insert
        // Rollback to index 0 should restore OldContent of the Update = "original"
        var result = await history.RollbackAsync("rb1", 0, "u");

        result.Content.Should().Be("original");
    }

    [Fact]
    public async Task RollbackAsync_OutOfRange_ShouldThrow()
    {
        var (store, history, _) = CreateServices();
        await store.CreateAsync(new CreateScriptRequest("rb2", "extract", "test", "body", null, "u", null));

        var act = () => history.RollbackAsync("rb2", 99, "u");

        await act.Should().ThrowAsync<RollbackConflictException>();
    }

    [Fact]
    public async Task RollbackAsync_NegativeIndex_ShouldThrow()
    {
        var (store, history, _) = CreateServices();
        await store.CreateAsync(new CreateScriptRequest("rb3", "extract", "test", "body", null, "u", null));

        var act = () => history.RollbackAsync("rb3", -1, "u");

        await act.Should().ThrowAsync<RollbackConflictException>();
    }

    [Fact]
    public async Task RollbackAsync_ScriptNotFound_ShouldThrow()
    {
        var (_, history, _) = CreateServices();
        var act = () => history.RollbackAsync("nonexistent", 0, "u");
        // Empty history → index 0 is out of range
        await act.Should().ThrowAsync<RollbackConflictException>();
    }
}
