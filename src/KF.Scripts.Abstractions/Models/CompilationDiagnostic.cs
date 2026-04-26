namespace KF.Scripts.Models;

/// <summary>A single diagnostic from script compilation.</summary>
public sealed record CompilationDiagnostic(
    DiagnosticSeverity Severity,
    string Message,
    int? Line,
    int? Column);
