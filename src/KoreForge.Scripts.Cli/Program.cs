using System.CommandLine;
using KoreForge.Scripts.Cli.Commands;

var rootCommand = new RootCommand("KoreForge Script Registry CLI")
{
    ListCommand.Create(),
    GetCommand.Create(),
    DownloadCommand.Create(),
    UploadCommand.Create(),
    DeleteCommand.Create(),
    HistoryCommand.Create(),
    RollbackCommand.Create(),
    ValidateCommand.Create(),
    ExportCommand.Create(),
    ImportCommand.Create()
};

rootCommand.AddGlobalOption(new Option<string>("--connection", "SQL Server connection string") { IsRequired = true });
rootCommand.AddGlobalOption(new Option<string>("--application", "Application ID") { IsRequired = true });
rootCommand.AddGlobalOption(new Option<string>("--format", () => "table", "Output format: table or json"));

return await rootCommand.InvokeAsync(args);
