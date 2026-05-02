using System.CommandLine;
using System.CommandLine.Invocation;
using KoreForge.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

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
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var id = ctx.ParseResult.GetValueForArgument((Argument<long>)cmd.Arguments.First());
            var rv = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "rowversion"))!;
            var user = ctx.ParseResult.GetValueForOption((Option<string>)cmd.Options.First(o => o.Name == "user"))!;

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            await store.DeleteAsync(id, user, Convert.FromBase64String(rv));
            Console.WriteLine($"Deleted script {id}");
        });

        return cmd;
    }
}
