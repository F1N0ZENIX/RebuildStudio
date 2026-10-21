using System.Text.Json;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Ai;

public static class PlanParser
{
    sealed class Dto { public string? Summary { get; set; } public List<PatchDto>? Patches { get; set; } }
    sealed class PatchDto { public string? File { get; set; } public string? Old { get; set; } public string? New { get; set; } }

    public static PatchPlan Parse(string? raw, string source)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Resposta da IA vazia.");
        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start < 0 || end <= start) throw new FormatException("A resposta da IA não contém JSON.");

        Dto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(raw[start..(end + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex) { throw new FormatException("JSON inválido na resposta da IA: " + ex.Message); }

        if (dto?.Patches is null || dto.Patches.Count == 0)
            throw new FormatException("A IA não propôs nenhuma alteração.");

        var patches = new List<FilePatch>();
        foreach (var p in dto.Patches)
        {
            if (string.IsNullOrWhiteSpace(p.File) || string.IsNullOrEmpty(p.Old) || p.New is null)
                throw new FormatException("Patch incompleto (file/old/new são obrigatórios).");
            patches.Add(new FilePatch(p.File, p.Old, p.New));
        }
        return new PatchPlan(dto.Summary ?? "(sem resumo)", patches, source);
    }
}
