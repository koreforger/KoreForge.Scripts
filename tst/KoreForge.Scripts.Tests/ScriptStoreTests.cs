using FluentAssertions;
using KoreForge.Scripts.Core.Services;
using KoreForge.Scripts.Exceptions;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Models;
using KoreForge.Scripts.Options;
using KoreForge.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace KoreForge.Scripts.Tests;

public sealed class ScriptStoreTests
{
    private readonly ScriptStoreOptions _options = new()
    {
        ConnectionString = "unused",
        ApplicationId = "TestApp"
    };

    private ScriptStore CreateStore(string? dbName = null)
    {
        var factory = TestDbFactory.CreateInMemory(dbName);
        IScriptCompiler[] compilers = [new PassThroughCompiler()];
        return new ScriptStore(factory, _options, compilers, NullLogger<ScriptStore>.Instance, UtcSystemClock.Instance);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateScript()
    {
        var store = CreateStore();
        var request = new CreateScriptRequest("test-script", "extract", "test", "content body", "desc", "user1", "initial");

        var result = await store.CreateAsync(request);

        result.ScriptId.Should().BeGreaterThan(0);
        result.Name.Should().Be("test-script");
        result.TypeTag.Should().Be("extract");
        result.Language.Should().Be("test");
        result.Content.Should().Be("content body");
        result.Description.Should().Be("desc");
        result.IsEnabled.Should().BeTrue();
        result.CreatedBy.Should().Be("user1");
        result.ApplicationId.Should().Be("TestApp");
    }

    [Fact]
    public async Task CreateAsync_WithCompilationError_ShouldThrow()
    {
        var store = CreateStore();
        var request = new CreateScriptRequest("bad-script", "extract", "test", "ERROR content", null, "user1", null);

        var act = () => store.CreateAsync(request);

        await act.Should().ThrowAsync<ScriptCompilationException>();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnScript()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("s1", "extract", "test", "body", null, "user1", null));

        var store2 = CreateStore(db);
        var result = await store2.GetByIdAsync(created.ScriptId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("s1");
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ShouldReturnNull()
    {
        var store = CreateStore();
        var result = await store.GetByIdAsync(9999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ShouldReturnScript()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        await store.CreateAsync(new CreateScriptRequest("find-me", "extract", "test", "body", null, "user1", null));

        var store2 = CreateStore(db);
        var result = await store2.GetByNameAsync("find-me");

        result.Should().NotBeNull();
        result!.Name.Should().Be("find-me");
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAll()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        await store.CreateAsync(new CreateScriptRequest("a", "extract", "test", "x", null, "u", null));
        await store.CreateAsync(new CreateScriptRequest("b", "transform", "test", "y", null, "u", null));

        var store2 = CreateStore(db);
        var result = await store2.ListAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_FilterByTypeTag_ShouldFilter()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        await store.CreateAsync(new CreateScriptRequest("a", "extract", "test", "x", null, "u", null));
        await store.CreateAsync(new CreateScriptRequest("b", "transform", "test", "y", null, "u", null));

        var store2 = CreateStore(db);
        var result = await store2.ListAsync(typeTag: "extract");
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("a");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateContent()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("upd", "extract", "test", "old", null, "u", null));

        var store2 = CreateStore(db);
        var updated = await store2.UpdateAsync(new UpdateScriptRequest(created.ScriptId, "new content", null, null, created.RowVersion, "u2", "updated"));

        updated.Content.Should().Be("new content");
        updated.ModifiedBy.Should().Be("u2");
    }

    [Fact]
    public async Task UpdateAsync_WrongRowVersion_ShouldThrowConcurrency()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("conc", "extract", "test", "body", null, "u", null));

        var store2 = CreateStore(db);
        var act = () => store2.UpdateAsync(new UpdateScriptRequest(created.ScriptId, "new", null, null, [0, 0, 0, 0, 0, 0, 0, 99], "u", null));

        await act.Should().ThrowAsync<ScriptConcurrencyException>();
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ShouldThrow()
    {
        var store = CreateStore();
        var act = () => store.UpdateAsync(new UpdateScriptRequest(9999, "x", null, null, [1], "u", null));
        await act.Should().ThrowAsync<ScriptNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WithCompilationError_ShouldThrow()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("comp", "extract", "test", "ok", null, "u", null));

        var store2 = CreateStore(db);
        var act = () => store2.UpdateAsync(new UpdateScriptRequest(created.ScriptId, "ERROR bad", null, null, created.RowVersion, "u", null));

        await act.Should().ThrowAsync<ScriptCompilationException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveScript()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("del", "extract", "test", "body", null, "u", null));

        var store2 = CreateStore(db);
        await store2.DeleteAsync(created.ScriptId, "u", created.RowVersion);

        var store3 = CreateStore(db);
        var result = await store3.GetByIdAsync(created.ScriptId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WrongRowVersion_ShouldThrowConcurrency()
    {
        var db = Guid.NewGuid().ToString();
        var store = CreateStore(db);
        var created = await store.CreateAsync(new CreateScriptRequest("del2", "extract", "test", "body", null, "u", null));

        var store2 = CreateStore(db);
        var act = () => store2.DeleteAsync(created.ScriptId, "u", [0, 0, 0, 0, 0, 0, 0, 99]);

        await act.Should().ThrowAsync<ScriptConcurrencyException>();
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ShouldThrow()
    {
        var store = CreateStore();
        var act = () => store.DeleteAsync(9999, "u", [1]);
        await act.Should().ThrowAsync<ScriptNotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_ContentTooLong_ShouldThrow()
    {
        var store = CreateStore();
        var longContent = new string('x', _options.MaxContentLength + 1);
        var request = new CreateScriptRequest("big", "extract", "test", longContent, null, "u", null);

        var act = () => store.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
