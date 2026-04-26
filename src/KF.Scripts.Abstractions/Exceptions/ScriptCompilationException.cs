using KF.Scripts.Models;

namespace KF.Scripts.Exceptions;

/// <summary>Thrown when script compilation fails.</summary>
public sealed class ScriptCompilationException : Exception
{
    public CompilationResult Result { get; }

    public ScriptCompilationException(CompilationResult result)
        : base($"Script compilation failed with {result.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s).")
    {
        Result = result;
    }
}
