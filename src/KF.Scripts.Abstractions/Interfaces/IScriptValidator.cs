using KF.Scripts.Models;

namespace KF.Scripts.Interfaces;

/// <summary>Validates script content without persisting.</summary>
public interface IScriptValidator
{
    Task<CompilationResult> ValidateAsync(string content, string language, CancellationToken ct = default);
}
