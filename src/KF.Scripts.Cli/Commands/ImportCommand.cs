using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using KF.Scripts.Interfaces;
using KF.Scripts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class ImportCommand
{
    public static Command Create()
    {
        var cmd = new Command("import", "Import scripts from a JSON file");
        cmd.AddOption(new Option<string>("--file", "Input JSON file path") { IsRequired = true });
        cmd.AddOption(new Option<bool>("--upsert", () => false, "Update existing scripts by name"));
        cmd.AddOption(new Option<string>("--user", () => "cli", "Changed by user"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var file = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "file") as Option<string>)!;
            var upsert = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "upsert") as Option<bool>);
            var user = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "user") as Option<string>)!;

            var json = await File.ReadAllTextAsync(file);
            var scripts = JsonSerializer.Deserialize<List<ScriptRecord>>(json)
                ?? throw new InvalidOperationException("Invalid JSON file.");

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();

            var created = 0;
            var updated = 0;
            var skipped = 0;

            foreach (var s in scripts)
            {
                var existing = await store.GetByNameAsync(s.Name);
                if (existing is not null)
                {
                    if (upsert)
                    {
                        await store.UpdateAsync(new UpdateScriptRequest(existing.ScriptId, s.Content, s.Description, s.IsEnabled, existing.RowVersion, user, "Imported"));
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else
                {
                    await store.CreateAsync(new CreateScriptRequest(s.Name, s.TypeTag, s.Language, s.Content, s.Description, user, "Imported"));
                    created++;
                }
            }

            Console.WriteLine($"Import complete: {created} created, {updated} updated, {skipped} skipped");
        });

        return cmd;
    }
}
