using System.CommandLine;
using System.CommandLine.Invocation;
using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

internal static class UploadCommand
{
    public static Command Create()
    {
        var cmd = new Command("upload", "Upload a script (create or update)");
        cmd.AddArgument(new Argument<string>("name", "Script name"));
        cmd.AddOption(new Option<string>("--file", "File path to upload") { IsRequired = true });
        cmd.AddOption(new Option<string>("--type", "TypeTag for new scripts") { IsRequired = true });
        cmd.AddOption(new Option<string>("--language", () => "jex", "Script language"));
        cmd.AddOption(new Option<string>("--user", () => "cli", "Changed by user"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var name = ctx.ParseResult.GetValueForArgument((Argument<string>)cmd.Arguments.First())!;
            var file = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "file"))!;
            var type = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "type"))!;
            var lang = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "language"))!;
            var user = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "user"))!;

            var content = await File.ReadAllTextAsync(file);

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            var existing = await store.GetByNameAsync(name);

            if (existing is not null)
            {
                var result = await store.UpdateAsync(new UpdateScriptRequest(existing.ScriptId, content, null, null, existing.RowVersion, user, $"Uploaded from {file}"));
                Console.WriteLine($"Updated '{name}' (Id={result.ScriptId})");
            }
            else
            {
                var result = await store.CreateAsync(new CreateScriptRequest(name, type, lang, content, null, user, $"Uploaded from {file}"));
                Console.WriteLine($"Created '{name}' (Id={result.ScriptId})");
            }
        });

        return cmd;
    }
}
