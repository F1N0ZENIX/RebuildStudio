using RebuildStudio.Core;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Tests;

public class WorkspaceManagerTests : IDisposable
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "rs-ws-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => WorkspaceManager.ForceDelete(_root);

    string NewFile(string content = "conteudo original")
    {
        Directory.CreateDirectory(_root);
        var p = Path.Combine(_root, "origem.bin");
        File.WriteAllText(p, content);
        return p;
    }

    [Fact]
    public void Cria_backup_e_copia_de_trabalho_sem_tocar_no_original()
    {
        var src = NewFile();
        var antes = Hashing.Sha256File(src);
        var s = new WorkspaceManager(Path.Combine(_root, "ws")).Create(src);

        Assert.Equal(antes, Hashing.Sha256File(s.BackupPath));
        Assert.Equal(antes, Hashing.Sha256File(s.WorkPath));
        Assert.Equal(antes, Hashing.Sha256File(src));
        Assert.True(new FileInfo(s.BackupPath).IsReadOnly);
        Assert.False(new FileInfo(s.WorkPath).IsReadOnly);
    }

    [Fact]
    public void Restaurar_desfaz_alteracoes_na_copia_de_trabalho()
    {
        var src = NewFile();
        var mgr = new WorkspaceManager(Path.Combine(_root, "ws"));
        var s = mgr.Create(src);

        File.WriteAllText(s.WorkPath, "alterado");
        mgr.Restore(s);

        Assert.Equal("conteudo original", File.ReadAllText(s.WorkPath));
    }

    [Fact]
    public void Diretorio_ignora_bin_e_obj()
    {
        var dir = Path.Combine(_root, "proj");
        Directory.CreateDirectory(Path.Combine(dir, "obj"));
        File.WriteAllText(Path.Combine(dir, "a.cs"), "x");
        File.WriteAllText(Path.Combine(dir, "obj", "lixo.txt"), "y");

        var s = new WorkspaceManager(Path.Combine(_root, "ws")).Create(dir);
        Assert.True(File.Exists(Path.Combine(s.WorkPath, "a.cs")));
        Assert.False(Directory.Exists(Path.Combine(s.WorkPath, "obj")));
    }

    [Theory]
    [InlineData("..\\fora.txt")]
    [InlineData("../../etc/passwd")]
    public void ResolveInside_bloqueia_saida_do_workspace(string relativo)
    {
        Assert.Throws<UnauthorizedAccessException>(() => WorkspaceManager.ResolveInside(_root, relativo));
    }

    [Fact]
    public void ResolveInside_aceita_caminho_interno()
    {
        var full = WorkspaceManager.ResolveInside(_root, Path.Combine("sub", "arq.cs"));
        Assert.StartsWith(Path.GetFullPath(_root), full);
    }
}
