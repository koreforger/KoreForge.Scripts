namespace KF.Scripts.Models;

/// <summary>Result of compiling a script.</summary>
public sealed record CompilationResult(
    bool Success,
    IReadOnlyList<CompilationDiagnostic> Diagnostics);
