using KF.Scripts.AspNet.Dtos;
using KF.Scripts.Exceptions;
using KF.Scripts.Interfaces;
using KF.Scripts.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.AspNet;

public static class ScriptEndpoints
{
    public static IEndpointRouteBuilder MapScriptEndpoints(this IEndpointRouteBuilder app, string prefix = "/api/scripts")
    {
        var group = app.MapGroup(prefix).WithTags("Scripts");

        group.MapGet("/", async (string? typeTag, bool? isEnabled, IScriptStore store, CancellationToken ct) =>
        {
            var scripts = await store.ListAsync(typeTag, isEnabled, ct);
            return Results.Ok(scripts);
        });

        group.MapGet("/{id:long}", async (long id, IScriptStore store, CancellationToken ct) =>
        {
            var script = await store.GetByIdAsync(id, ct);
            return script is null ? Results.NotFound() : Results.Ok(script);
        });

        group.MapGet("/by-name/{name}", async (string name, IScriptStore store, CancellationToken ct) =>
        {
            var script = await store.GetByNameAsync(name, ct);
            return script is null ? Results.NotFound() : Results.Ok(script);
        });

        group.MapPost("/", async (CreateScriptDto dto, IScriptStore store, CancellationToken ct) =>
        {
            try
            {
                var request = new CreateScriptRequest(dto.Name, dto.TypeTag, dto.Language, dto.Content, dto.Description, dto.CreatedBy, dto.Comment);
                var script = await store.CreateAsync(request, ct);
                return Results.Created($"{prefix}/{script.ScriptId}", script);
            }
            catch (ScriptCompilationException ex)
            {
                return Results.BadRequest(new { error = "Compilation failed", diagnostics = ex.Result.Diagnostics });
            }
        });

        group.MapPut("/{id:long}", async ([FromRoute] long id, [FromBody] UpdateScriptDto dto, IScriptStore store, CancellationToken ct) =>
        {
            try
            {
                var rowVersion = Convert.FromBase64String(dto.RowVersion);
                var request = new UpdateScriptRequest(id, dto.Content, dto.Description, dto.IsEnabled, rowVersion, dto.ModifiedBy, dto.Comment);
                var script = await store.UpdateAsync(request, ct);
                return Results.Ok(script);
            }
            catch (ScriptNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ScriptConcurrencyException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ScriptCompilationException ex)
            {
                return Results.BadRequest(new { error = "Compilation failed", diagnostics = ex.Result.Diagnostics });
            }
        });

        group.MapDelete("/{id:long}", async ([FromRoute] long id, [FromBody] DeleteScriptDto dto, IScriptStore store, CancellationToken ct) =>
        {
            try
            {
                var rowVersion = Convert.FromBase64String(dto.RowVersion);
                await store.DeleteAsync(id, dto.ChangedBy, rowVersion, ct);
                return Results.NoContent();
            }
            catch (ScriptNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ScriptConcurrencyException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapGet("/{id:long}/history", async (long id, IScriptStore store, IScriptHistoryService history, CancellationToken ct) =>
        {
            var script = await store.GetByIdAsync(id, ct);
            if (script is null) return Results.NotFound();
            var records = await history.GetHistoryAsync(script.Name, ct);
            return Results.Ok(records);
        });

        group.MapPost("/by-name/{name}/rollback", async ([FromRoute] string name, [FromBody] RollbackDto dto, IScriptHistoryService history, CancellationToken ct) =>
        {
            try
            {
                var result = await history.RollbackAsync(name, dto.VersionIndex, dto.ChangedBy, ct);
                return Results.Ok(result);
            }
            catch (ScriptNotFoundException)
            {
                return Results.NotFound();
            }
            catch (RollbackConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        group.MapPost("/validate", async (ValidateScriptDto dto, IScriptValidator validator, CancellationToken ct) =>
        {
            var result = await validator.ValidateAsync(dto.Content, dto.Language, ct);
            return Results.Ok(result);
        });

        return app;
    }
}
