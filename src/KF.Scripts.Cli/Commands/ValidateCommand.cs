using System.CommandLine;
using System.CommandLine.Invocation;
using KF.Scripts.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Cli.Commands;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var cmd = new Command("validate", "Validate a script file without saving");
        cmd.AddOption(new Option<string>("--file", "File path to validate") { IsRequired = true });
        cmd.AddOption(new Option<string>("--language", () => "jex", "Script language"));

        cmd.SetHandler(async (InvocationContext ctx) =>
        {
            var conn = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "connection") as Option<string>)!;
            var app = ctx.ParseResult.GetValueForOption(ctx.ParseResult.RootCommandResult.Command.Options.First(o => o.Name == "application") as Option<string>)!;
            var file = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "file") as Option<string>)!;
            var lang = ctx.ParseResult.GetValueForOption(cmd.Options.First(o => o.Name == "language") as Option<string>)!;

            var content = await File.ReadAllTextAsync(file);

            await using var sp = ServiceFactory.Build(conn, app);
            using var scope = sp.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IScriptValidator>();
            var result = await validator.ValidateAsync(content, lang);

            if (result.Success)
            {
                Console.WriteLine("Validation passed.");
            }
            else
            {
                Console.Error.WriteLine("Validation failed:");
                foreach (var d in result.Diagnostics)
                    Console.Error.WriteLine($"  [{d.Severity}] Line {d.Line}: {d.Message}");
                ctx.ExitCode = 1;
            }
        });

        return cmd;
    }
}
