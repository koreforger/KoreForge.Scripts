using System.CommandLine;
using System.CommandLine.Invocation;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class DeleteCommand
{
    public static Command Create()
    {
        var cmd = new Command("delete", "Delete a script by ID");
        cmd.AddArgument(new Argument<long>("id", "Script ID"));
        cmd.AddOption(new Option<string>("--rowversion", "Base64 RowVersion") { IsRequired = true });
        cmd.AddOption(new Option<string>("--user", () => "cli", "Changed by user"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var id = ctx.ParseResult.GetValueForArgument(cmd.Arguments.First() as Argument<long>);
            var rv = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "rowversion") as Option<string>)!;
            var user = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "user") as Option<string>)!;

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            await store.DeleteAsync(id, user, Convert.FromBase64String(rv));
            Console.WriteLine($"Deleted script {id}");
        });

        return cmd;
    }
}
