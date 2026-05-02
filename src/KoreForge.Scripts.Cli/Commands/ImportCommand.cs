using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

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
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var file = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "file"))!;
            var upsert = ctx.ParseResult.GetValueForOption((Option<bool>)cmd.Options.First(o => o.Name == "upsert"));
            var user = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "user"))!;

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
