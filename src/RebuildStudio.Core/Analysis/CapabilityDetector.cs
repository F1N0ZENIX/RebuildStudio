namespace RebuildStudio.Core.Analysis;

public enum Feasibility { None, AnalysisOnly, Limited }

public sealed record CapabilityAssessment(
    Feasibility Level,
    string Summary,
    IReadOnlyList<string> Available,
    IReadOnlyList<string> Unavailable)
{
    public string Label => Level switch
    {
        Feasibility.None => "Inviável",
        Feasibility.AnalysisOnly => "Somente análise",
        _ => "Parcial"
    };
}

public sealed class CapabilityDetector
{
    public CapabilityAssessment Assess(PeReport r)
    {
        if (!r.IsPe || r.Kind == ExecutableKind.NotPe)
            return new(Feasibility.None, "Não é um executável PE válido; nenhum arquivo será gerado.",
                Array.Empty<string>(), new[] { "Qualquer modificação" });

        return r.Kind switch
        {
            ExecutableKind.DotNet => new(Feasibility.Limited,
                "Assembly .NET: análise e descompilação são possíveis; recompilar este EXE específico não é garantido.",
                new[]
                {
                    "Análise de metadados e hash",
                    "Descompilar para projeto C# com ILSpy (ilspycmd, ferramenta externa opcional)",
                    "Modificar e compilar projetos-fonte .NET (fluxo demonstrado com o projeto de teste)"
                },
                new[]
                {
                    "Recompilar este EXE automaticamente a partir da descompilação (não garantido; fora do MVP)",
                    "Patch de IL direto no binário (não implementado)"
                }),
            ExecutableKind.Electron => new(Feasibility.AnalysisOnly,
                "Electron detectado. Nesta versão há apenas detecção.",
                new[] { "Detecção do pacote e hash" },
                new[] { "Edição e reempacotamento de app.asar (planejado, não implementado)" }),
            _ => new(Feasibility.AnalysisOnly,
                "Binário nativo: somente análise estática do formato PE.",
                new[] { "Cabeçalhos PE, seções, arquitetura e hash" },
                new[]
                {
                    "Recuperar código-fonte original",
                    "Edição de recursos PE (planejado, não implementado)",
                    "Recompilação"
                })
        };
    }
}
