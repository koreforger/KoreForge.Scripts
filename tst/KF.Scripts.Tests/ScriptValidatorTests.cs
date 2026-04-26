using FluentAssertions;
using KF.Scripts.Core.Services;
using KF.Scripts.Models;

namespace KF.Scripts.Tests;

public sealed class ScriptValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WithRegisteredCompiler_ShouldReturnResult()
    {
        var compilers = new[] { new PassThroughCompiler() };
        var validator = new ScriptValidator(compilers);

        var result = await validator.ValidateAsync("valid content", "test");

        result.Success.Should().BeTrue();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithCompilationError_ShouldReturnFailure()
    {
        var compilers = new[] { new PassThroughCompiler() };
        var validator = new ScriptValidator(compilers);

        var result = await validator.ValidateAsync("ERROR content", "test");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ValidateAsync_UnknownLanguage_ShouldReturnError()
    {
        var compilers = new[] { new PassThroughCompiler() };
        var validator = new ScriptValidator(compilers);

        var result = await validator.ValidateAsync("content", "unknown-lang");

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().HaveCount(1);
        result.Diagnostics[0].Message.Should().Contain("unknown-lang");
    }
}
