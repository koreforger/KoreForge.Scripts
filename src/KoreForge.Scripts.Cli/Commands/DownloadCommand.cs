using System.CommandLine;
using System.CommandLine.Invocation;
using KoreForge.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KoreForge.Scripts.Cli.Commands;

internal static class DownloadCommand
{
    public static Command Create()
    {
        var cmd = new Command("download", "Download script content to a file");
        cmd.AddArgument(new Argument<string>("name", "Script name"));
        cmd.AddOption(new Option<string?>("--output", "Output file path (default: stdout)"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection"))!;
            var app = ctx.ParseResult.GetValueForOption((Option<string>)ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application"))!;
            var name = ctx.ParseResult.GetValueForArgument((Argument<string>)cmd.Arguments.First())!;
            var output = ctx.ParseResult.GetValueForOption((Option<string?>)cmd.Options.First(o => o.Name == "output"));

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IScriptStore>();
            var script = await store.GetByNameAsync(name);

            if (script is null)
            {
                Console.Error.WriteLine($"Script '{name}' not found.");
                ctx.ExitCode = 1;
                return;
            }

            if (output is not null)
            {
                await File.WriteAllTextAsync(output, script.Content);
                Console.WriteLine($"Downloaded '{name}' to {output}");
            }
            else
            {
                Console.Write(script.Content);
            }
        });

        return cmd;
    }
}
