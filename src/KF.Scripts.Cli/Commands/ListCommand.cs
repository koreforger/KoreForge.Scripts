using System.CommandLine;
using System.CommandLine.Invocation;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class ListCommand
{
    public static Command Create()
    {
        var cmd = new Command("list", "List all scripts");
        cmd.AddOption(new Option<string?>("--type", "Filter by TypeTag"));
        cmd.AddOption(new Option<bool?>("--enabled", "Filter by IsEnabled"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var typeTag = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "type") as Option<string?>);
            var enabled = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "enabled") as Option<bool?>);

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            var scripts = await store.ListAsync(typeTag, enabled);

            Console.WriteLine($"{"Id",-8} {"Name",-30} {"Type",-15} {"Lang",-8} {"Enabled",-8}");
            Console.WriteLine(new string('-', 75));
            foreach (var s in scripts)
                Console.WriteLine($"{s.ScriptId,-8} {s.Name,-30} {s.TypeTag,-15} {s.Language,-8} {s.IsEnabled,-8}");
        });

        return cmd;
    }
}
