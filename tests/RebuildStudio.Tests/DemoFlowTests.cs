using RebuildStudio.Core.Build;
using RebuildStudio.Core.Demo;
using RebuildStudio.Core.Release;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Tests;

public class DemoFlowTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "rs-flow-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => WorkspaceManager.ForceDelete(_root);

    [Fact]
    public void ManualPlan_escapa_aspas()
    {
        var plan = DemoProject.ManualPlan("diga \"oi\"");
        Assert.Contains("\\\"oi\\\"", plan.Patches[0].New);
    }

    [Fact]
    public void ManualPlan_rejeita_texto_vazio() =>
        Assert.Throws<ArgumentException>(() => DemoProject.ManualPlan("   "));

    [Fact]
    public async Task Fluxo_completo_modifica_compila_valida_e_exporta()
    {
        var sample = DemoProject.FindSampleDir(AppContext.BaseDirectory);
        Assert.NotNull(sample);

        var ws = new WorkspaceManager(Path.Combine(_root, "ws"));
        var s = ws.Create(sample!);
        var plan = DemoProject.ManualPlan("Olá do teste automatizado!");
        var applyLog = new PatchApplier().Apply(s, plan);

        var build = await new DotNetBuildRunner().BuildAsync(s, DemoProject.ProjectFile, null, CancellationToken.None);
        Assert.True(build.Success, string.Join("\n", build.Log.TakeLast(30)));

        var val = ArtifactValidator.ValidateAssembly(
            Path.Combine(build.OutputDir, DemoProject.AssemblyFile),
            plan.ExpectedRuntimeText, plan.ForbiddenRuntimeText);
        Assert.True(val.Passed, string.Join("\n", val.Checks));

        // O projeto original não pode ter sido alterado.
        Assert.Contains(DemoProject.OriginalGreeting,
            File.ReadAllText(Path.Combine(sample!, DemoProject.ProgramFile)));

        var dest = new ReleaseExporter().Export(
            new RunReport(s, plan, applyLog, build, val, DateTimeOffset.Now), Path.Combine(_root, "export"));
        Assert.True(File.Exists(Path.Combine(dest, "relatorio.md")));
        Assert.True(File.Exists(Path.Combine(dest, "artefatos", DemoProject.AssemblyFile)));
    }

    [Fact]
    public async Task Erro_de_compilacao_nunca_e_reportado_como_sucesso()
    {
        var sample = DemoProject.FindSampleDir(AppContext.BaseDirectory)!;
        var s = new WorkspaceManager(Path.Combine(_root, "ws")).Create(sample);
        // Quebra o código de propósito.
        var plan = new PatchPlan("quebrar", new[]
        {
            new FilePatch(DemoProject.ProgramFile, "Console.WriteLine(Greeting);", "Console.WriteLine(Greeting")
        }, "teste");
        new PatchApplier().Apply(s, plan);

        var build = await new DotNetBuildRunner().BuildAsync(s, DemoProject.ProjectFile, null, CancellationToken.None);
        Assert.False(build.Success);
        Assert.NotEmpty(build.Errors);
    }

    [Fact]
    public void Exportacao_exige_build_validado()
    {
        var sample = DemoProject.FindSampleDir(AppContext.BaseDirectory)!;
        var s = new WorkspaceManager(Path.Combine(_root, "ws")).Create(sample);
        var report = new RunReport(s, null, Array.Empty<string>(), null, null, DateTimeOffset.Now);
        Assert.Throws<InvalidOperationException>(() => new ReleaseExporter().Export(report, Path.Combine(_root, "export")));
    }
}
