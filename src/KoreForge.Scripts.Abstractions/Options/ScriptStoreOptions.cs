namespace KoreForge.Scripts.Options;

/// <summary>Configuration options for the script store.</summary>
public sealed class ScriptStoreOptions
{
    public string? ConnectionString { get; set; }
    public string? ApplicationId { get; set; }
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxContentLength { get; set; } = 1_048_576; // 1 MB
}
