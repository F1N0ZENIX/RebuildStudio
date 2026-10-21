using RebuildStudio.Core.Analysis;

namespace RebuildStudio.Tests;

public class CapabilityDetectorTests
{
    static PeReport Report(bool isPe, ExecutableKind kind) =>
        new("x.exe", 1, new string('0', 64), isPe, false, PeArchitecture.X64, kind, null, null, null,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void Nao_PE_e_inviavel() =>
        Assert.Equal(Feasibility.None, new CapabilityDetector().Assess(Report(false, ExecutableKind.NotPe)).Level);

    [Fact]
    public void DotNet_e_parcial_e_nao_promete_recompilacao()
    {
        var a = new CapabilityDetector().Assess(Report(true, ExecutableKind.DotNet));
        Assert.Equal(Feasibility.Limited, a.Level);
        Assert.Contains(a.Unavailable, u => u.Contains("Recompilar este EXE", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ExecutableKind.Native)]
    [InlineData(ExecutableKind.Electron)]
    public void Nativo_e_electron_sao_somente_analise(ExecutableKind kind) =>
        Assert.Equal(Feasibility.AnalysisOnly, new CapabilityDetector().Assess(Report(true, kind)).Level);
}
