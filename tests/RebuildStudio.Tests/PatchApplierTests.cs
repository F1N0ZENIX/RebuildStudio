using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Tests;

public class PatchApplierTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "rs-patch-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => WorkspaceManager.ForceDelete(_root);

    WorkspaceSession Session(params (string name, string content)[] files)
    {
        var src = Path.Combine(_root, "src");
        Directory.CreateDirectory(src);
        foreach (var (n, c) in files) File.WriteAllText(Path.Combine(src, n), c);
        return new WorkspaceManager(Path.Combine(_root, "ws")).Create(src);
    }

    static PatchPlan Plan(string file, string old, string @new) =>
        new("teste", new[] { new FilePatch(file, old, @new) }, "teste");

    [Fact]
    public void Aplica_na_copia_de_trabalho_e_preserva_o_backup()
    {
        var s = Session(("Program.cs", "var a = \"antigo\";"));
        new PatchApplier().Apply(s, Plan("Program.cs", "antigo", "novo"));

        Assert.Contains("novo", File.ReadAllText(Path.Combine(s.WorkPath, "Program.cs")));
        Assert.Contains("antigo", File.ReadAllText(Path.Combine(s.BackupPath, "Program.cs")));
    }

    [Fact]
    public void Rejeita_trecho_ambiguo()
    {
        var s = Session(("a.cs", "x x"));
        Assert.Throws<InvalidOperationException>(() => new PatchApplier().Apply(s, Plan("a.cs", "x", "y")));
    }

    [Fact]
    public void Rejeita_trecho_inexistente()
    {
        var s = Session(("a.cs", "abc"));
        Assert.Throws<InvalidOperationException>(() => new PatchApplier().Apply(s, Plan("a.cs", "zzz", "y")));
    }

    [Fact]
    public void Rejeita_caminho_fora_do_workspace()
    {
        var s = Session(("a.cs", "abc"));
        Assert.Throws<UnauthorizedAccessException>(() => new PatchApplier().Apply(s, Plan("..\\..\\fora.cs", "abc", "y")));
    }

    [Fact]
    public void Rejeita_arquivos_de_build()
    {
        var s = Session(("App.csproj", "<Project/>"));
        Assert.Throws<InvalidOperationException>(() => new PatchApplier().Apply(s, Plan("App.csproj", "<Project/>", "<Project><Target/></Project>")));
    }

    [Fact]
    public void E_atomico_se_um_patch_falha_nada_e_gravado()
    {
        var s = Session(("a.cs", "um"), ("b.cs", "dois"));
        var plan = new PatchPlan("x", new[]
        {
            new FilePatch("a.cs", "um", "UM"),
            new FilePatch("b.cs", "inexistente", "x")
        }, "teste");

        Assert.Throws<InvalidOperationException>(() => new PatchApplier().Apply(s, plan));
        Assert.Equal("um", File.ReadAllText(Path.Combine(s.WorkPath, "a.cs")));
    }
}
