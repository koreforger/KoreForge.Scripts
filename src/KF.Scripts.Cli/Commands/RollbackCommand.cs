using System.CommandLine;
using System.CommandLine.Invocation;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

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
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var name = ctx.ParseResult.GetValueForArgument(cmd.Arguments.First(a => a.Name == "name") as Argument<string>)!;
            var idx = ctx.ParseResult.GetValueForArgument(cmd.Arguments.First(a => a.Name == "versionIndex") as Argument<int>);
            var user = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "user") as Option<string>)!;

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var history = scope.ServiceProvider.GetRequiredService<IScriptHistoryService>();
            var result = await history.RollbackAsync(name, idx, user);
            Console.WriteLine($"Rolled back '{name}' to version index {idx} (Id={result.ScriptId})");
        });

        return cmd;
    }
}
