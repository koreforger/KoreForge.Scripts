using KF.Scripts.Models;

namespace KF.Scripts.Interfaces;

/// <summary>Compiles script content for a specific language.</summary>
public interface IScriptCompiler
{
    string Language { get; }
    Task<CompilationResult> CompileAsync(string content, CancellationToken ct = default);
}
