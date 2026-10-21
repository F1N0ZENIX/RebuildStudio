namespace RebuildStudio.Core.Analysis;

public enum PeArchitecture { Unknown, X86, X64, Arm, Arm64, AnyCpu }

public enum ExecutableKind { Unknown, NotPe, DotNet, Electron, Native }

public sealed record PeReport(
    string FilePath,
    long SizeBytes,
    string Sha256,
    bool IsPe,
    bool IsDll,
    PeArchitecture Architecture,
    ExecutableKind Kind,
    string? ManagedAssemblyPath,
    string? AssemblyName,
    string? ClrMetadataVersion,
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> Notes,
    IReadOnlyList<string> Limitations);
