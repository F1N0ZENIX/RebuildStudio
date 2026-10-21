using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace RebuildStudio.Core.Analysis;

/// <summary>Análise estática (somente leitura). Nunca executa o arquivo analisado.</summary>
public sealed class PeAnalyzer
{
    public PeReport Analyze(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Arquivo não encontrado.", path);

        var size = new FileInfo(path).Length;
        var sha = Hashing.Sha256File(path);
        var notes = new List<string>();
        var limits = new List<string>();

        try
        {
            using var fs = File.OpenRead(path);
            using var pe = new PEReader(fs);
            var h = pe.PEHeaders;
            if (h.PEHeader is null)
                return NotPe(path, size, sha, "Cabeçalho PE opcional ausente; não é um executável/DLL PE.");

            var is64 = h.PEHeader.Magic == PEMagic.PE32Plus;
            var arch = MapMachine(h.CoffHeader.Machine);
            var sections = h.SectionHeaders.Select(s => s.Name).ToList();

            string? managedPath = null, asmName = null, clr = null;
            var kind = ExecutableKind.Native;

            if (h.CorHeader is not null && pe.HasMetadata)
            {
                kind = ExecutableKind.DotNet;
                managedPath = path;
                var md = pe.GetMetadataReader();
                clr = md.MetadataVersion;
                if (md.IsAssembly) asmName = md.GetString(md.GetAssemblyDefinition().Name);

                var f = h.CorHeader.Flags;
                if ((f & CorFlags.ILOnly) == 0)
                    notes.Add("Assembly misto (IL + código nativo): modificação e recompilação são mais difíceis.");
                if (!is64 && h.CoffHeader.Machine == Machine.I386
                    && (f & CorFlags.ILOnly) != 0 && (f & CorFlags.Requires32Bit) == 0)
                    arch = PeArchitecture.AnyCpu;
                if ((f & CorFlags.StrongNameSigned) != 0)
                    limits.Add("Assembly com strong name: qualquer modificação invalida a assinatura original.");
            }
            else
            {
                var sibling = Path.ChangeExtension(path, ".dll");
                if (File.Exists(sibling) && IsManaged(sibling))
                {
                    kind = ExecutableKind.DotNet;
                    managedPath = sibling;
                    notes.Add($"Apphost .NET: o código gerenciado está em {Path.GetFileName(sibling)}.");
                }
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            if (File.Exists(Path.Combine(dir, "resources", "app.asar"))
                || Directory.Exists(Path.Combine(dir, "resources", "app")))
            {
                kind = ExecutableKind.Electron;
                notes.Add("Aplicativo Electron detectado (pasta resources/ com app.asar ou app/).");
            }

            if (kind == ExecutableKind.Native)
            {
                limits.Add("Binário nativo: o código-fonte original não pode ser recuperado automaticamente.");
                limits.Add("Bundles single-file .NET e executáveis empacotados (packers) podem aparecer aqui como nativos.");
            }

            return new PeReport(path, size, sha, true, h.IsDll, arch, kind, managedPath, asmName, clr,
                sections, notes, limits);
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException)
        {
            return NotPe(path, size, sha, "Formato PE inválido: " + ex.Message);
        }
    }

    static PeReport NotPe(string path, long size, string sha, string reason) =>
        new(path, size, sha, false, false, PeArchitecture.Unknown, ExecutableKind.NotPe, null, null, null,
            Array.Empty<string>(), Array.Empty<string>(), new[] { reason });

    static bool IsManaged(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var pe = new PEReader(fs);
            return pe.PEHeaders.CorHeader is not null && pe.HasMetadata;
        }
        catch { return false; }
    }

    static PeArchitecture MapMachine(Machine m) => m switch
    {
        Machine.I386 => PeArchitecture.X86,
        Machine.Amd64 => PeArchitecture.X64,
        Machine.Arm64 => PeArchitecture.Arm64,
        Machine.Arm or Machine.ArmThumb2 => PeArchitecture.Arm,
        _ => PeArchitecture.Unknown
    };
}
