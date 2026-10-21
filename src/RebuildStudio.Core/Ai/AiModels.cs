using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Ai;

public sealed record SourceFile(string RelativePath, string Content);

public sealed record PlanRequest(string UserRequest, IReadOnlyList<SourceFile> Files, string Model);

public interface IAiProvider
{
    string Name { get; }
    Task<PatchPlan> PlanAsync(PlanRequest request, CancellationToken ct);
}

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message) : base(message) { }
}
