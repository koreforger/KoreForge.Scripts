using KF.Scripts.Interfaces;
using KF.Scripts.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KF.Scripts.Core.Services;

public sealed class ScriptValidator : IScriptValidator
{
    private readonly IEnumerable<IScriptCompiler> _compilers;

    public ScriptValidator(IEnumerable<IScriptCompiler> compilers)
    {
        _compilers = compilers;
    }

    public async Task<CompilationResult> ValidateAsync(string content, string language, CancellationToken ct = default)
    {
        var compiler = _compilers.FirstOrDefault(c => c.Language.Equals(language, StringComparison.OrdinalIgnoreCase));

        if (compiler is null)
            return new CompilationResult(false, [new CompilationDiagnostic(
                DiagnosticSeverity.Error,
                $"No compiler registered for language '{language}'.",
                null, null)]);

        return await compiler.CompileAsync(content, ct);
    }
}
