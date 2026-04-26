using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class GetCommand
{
    public static Command Create()
    {
        var cmd = new Command("get", "Get a script by ID");
        cmd.AddArgument(new Argument<long>("id", "Script ID"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var id = ctx.ParseResult.GetValueForArgument(cmd.Arguments.First() as Argument<long>);

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            var script = await store.GetByIdAsync(id);

            if (script is null)
            {
                Console.Error.WriteLine($"Script {id} not found.");
                ctx.ExitCode = 1;
                return;
            }

            Console.WriteLine(JsonSerializer.Serialize(script, new JsonSerializerOptions { WriteIndented = true }));
        });

        return cmd;
    }
}
