using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class ExportCommand
{
    public static Command Create()
    {
        var cmd = new Command("export", "Export all scripts to a JSON file");
        cmd.AddOption(new Option<string>("--output", "Output file path") { IsRequired = true });
        cmd.AddOption(new Option<string?>("--type", "Filter by TypeTag"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var output = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "output") as Option<string>)!;
            var type = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "type") as Option<string?>);

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            var scripts = await store.ListAsync(type);

            var json = JsonSerializer.Serialize(scripts, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(output, json);
            Console.WriteLine($"Exported {scripts.Count} script(s) to {output}");
        });

        return cmd;
    }
}
