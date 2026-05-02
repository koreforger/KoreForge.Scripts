using KoreForge.Scripts.Interfaces;
using KoreForge.Scripts.Models;

namespace KoreForge.Scripts.Tests;

internal sealed class PassThroughCompiler : IScriptCompiler
{
    public string Language => "test";
    public Task<CompilationResult> CompileAsync(string content, CancellationToken ct = default)
    {
        if (content.Contains("ERROR"))
            return Task.FromResult(new CompilationResult(false,
                [new CompilationDiagnostic(DiagnosticSeverity.Error, "Script contains ERROR", 1, 1)]));

        return Task.FromResult(new CompilationResult(true, []));
    }
}
