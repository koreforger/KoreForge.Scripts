using System.CommandLine;
using System.CommandLine.Invocation;
using KoreForge.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

internal static class HistoryCommand
{
    public static Command Create()
    {
        var cmd = new Command("history", "Show history for a script");
        cmd.AddArgument(new Argument<string>("name", "Script name"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var name = ctx.ParseResult.GetValueForArgument((Argument<string>)cmd.Arguments.First())!;

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var history = scope.ServiceProvider.GetRequiredService<IScriptHistoryService>();
            var records = await history.GetHistoryAsync(name);

            Console.WriteLine($"{"Idx",-5} {"Operation",-12} {"ChangedBy",-15} {"Date",-22}");
            Console.WriteLine(new string('-', 60));
            for (var i = 0; i < records.Count; i++)
            {
                var r = records[i];
                Console.WriteLine($"{i,-5} {r.Operation,-12} {r.ChangedBy,-15} {r.ChangedDate:yyyy-MM-dd HH:mm:ss}");
            }
        });

        return cmd;
    }
}
