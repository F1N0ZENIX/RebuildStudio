using RebuildStudio.Core.Analysis;

namespace RebuildStudio.Tests;

public class PeAnalyzerTests : IDisposable
{
    readonly string _tmp = Path.Combine(Path.GetTempPath(), "rs-pe-" + Guid.NewGuid().ToString("N"));

    public PeAnalyzerTests() => Directory.CreateDirectory(_tmp);
    public void Dispose() => Directory.Delete(_tmp, true);

    static string CoreDll => typeof(PeAnalyzer).Assembly.Location;

    [Fact]
    public void Identifica_assembly_dotnet()
    {
        var r = new PeAnalyzer().Analyze(CoreDll);
        Assert.True(r.IsPe);
        Assert.True(r.IsDll);
        Assert.Equal(ExecutableKind.DotNet, r.Kind);
        Assert.Equal("RebuildStudio.Core", r.AssemblyName);
        Assert.NotEqual(PeArchitecture.Unknown, r.Architecture);
        Assert.Equal(64, r.Sha256.Length);
        Assert.Contains(".text", r.Sections);
    }

    [Fact]
    public void Arquivo_que_nao_e_PE_e_rejeitado()
    {
        var p = Path.Combine(_tmp, "falso.exe");
        File.WriteAllText(p, "isto não é um executável");
        var r = new PeAnalyzer().Analyze(p);
        Assert.False(r.IsPe);
        Assert.Equal(ExecutableKind.NotPe, r.Kind);
        Assert.NotEmpty(r.Limitations);
    }

    [Fact]
    public void Arquivo_inexistente_lanca_excecao()
    {
        Assert.Throws<FileNotFoundException>(() => new PeAnalyzer().Analyze(Path.Combine(_tmp, "nao-existe.exe")));
    }

    [Fact]
    public void Detecta_electron_pela_pasta_resources()
    {
        var exe = Path.Combine(_tmp, "app.exe");
        File.Copy(CoreDll, exe);
        Directory.CreateDirectory(Path.Combine(_tmp, "resources"));
        File.WriteAllText(Path.Combine(_tmp, "resources", "app.asar"), "x");
        var r = new PeAnalyzer().Analyze(exe);
        Assert.Equal(ExecutableKind.Electron, r.Kind);
    }

    [Fact]
    public void Analise_nao_altera_o_arquivo()
    {
        var p = Path.Combine(_tmp, "copia.dll");
        File.Copy(CoreDll, p);
        var antes = RebuildStudio.Core.Hashing.Sha256File(p);
        new PeAnalyzer().Analyze(p);
        Assert.Equal(antes, RebuildStudio.Core.Hashing.Sha256File(p));
    }
}
