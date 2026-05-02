using System.CommandLine;
using System.CommandLine.Invocation;
using KoreForge.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

internal static class RollbackCommand
{
    public static Command Create()
    {
        var cmd = new Command("rollback", "Rollback a script to a previous version");
        cmd.AddArgument(new Argument<string>("name", "Script name"));
        cmd.AddArgument(new Argument<int>("versionIndex", "History version index to rollback to"));
        cmd.AddOption(new Option<string>("--user", () => "cli", "Changed by user"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var name = ctx.ParseResult.GetValueForArgument((Argument<string>)cmd.Arguments.First(a => a.Name == "name"))!;
            var idx = ctx.ParseResult.GetValueForArgument((Argument<int>)cmd.Arguments.First(a => a.Name == "versionIndex"));
            var user = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "user"))!;

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var history = scope.ServiceProvider.GetRequiredService<IScriptHistoryService>();
            var result = await history.RollbackAsync(name, idx, user);
            Console.WriteLine($"Rolled back '{name}' to version index {idx} (Id={result.ScriptId})");
        });

        return cmd;
    }
}
