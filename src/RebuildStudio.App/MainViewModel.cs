using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;
using RebuildStudio.Core;
using RebuildStudio.Core.Ai;
using RebuildStudio.Core.Analysis;
using RebuildStudio.Core.Build;
using RebuildStudio.Core.Demo;
using RebuildStudio.Core.Release;
using RebuildStudio.Core.Security;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.App;

public enum StudioState
{
    Idle, Analyzing, Analyzed, Preparing, Prepared, Planning, Planned,
    Applying, Applied, Building, Succeeded, Failed
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    const string ProviderKey = "openrouter";

    readonly IUserPrompts _prompts;
    readonly SynchronizationContext _ui;
    readonly PeAnalyzer _analyzer = new();
    readonly CapabilityDetector _detector = new();
    readonly PatchApplier _patcher = new();
    readonly DotNetBuildRunner _builder = new();
    readonly IlSpyAdapter _ilspy = new();
    readonly WorkspaceManager _workspaces;
    readonly ApiKeyStore _keys;
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };

    WorkspaceSession? _analysisSession;
    WorkspaceSession? _demoSession;
    PeReport? _report;
    PatchPlan? _plan;
    IReadOnlyList<string> _applyLog = Array.Empty<string>();
    BuildResult? _build;
    ValidationResult? _validation;
    bool _applied;
    string? _logFile;

    string _selectedPath = "";
    string _reportText = "";
    string _feasibilityText = "Viabilidade: —";
    Brush _feasibilityBrush = Brushes.Gray;
    string _requestText = "Olá do Rebuild Studio AI!";
    string _modelName = "openai/gpt-4o-mini";
    bool _useAi;
    bool _hasApiKey;
    string _planText = "";
    string _stateText = "Pronto";
    string _logText = "";

    public MainViewModel(IUserPrompts prompts)
    {
        _prompts = prompts;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        var data = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RebuildStudioAI");
        _workspaces = new WorkspaceManager(Path.Combine(data, "workspaces"));
        _keys = new ApiKeyStore(Path.Combine(data, "keys"));
        HasApiKey = _keys.Has(ProviderKey);

        SelectExeCommand = new(() => { var p = _prompts.PickExecutable(); if (p is not null) SelectedPath = p; return Task.CompletedTask; });
        AnalyzeCommand = new(AnalyzeAsync, () => !string.IsNullOrWhiteSpace(SelectedPath));
        DecompileCommand = new(DecompileAsync, () => _report?.Kind == ExecutableKind.DotNet);
        PrepareDemoCommand = new(PrepareDemoAsync);
        PlanCommand = new(PlanAsync, () => _demoSession is not null);
        ApplyCommand = new(ApplyAsync, () => _plan is not null && _demoSession is not null && !_applied);
        BuildCommand = new(BuildAsync, () => _applied);
        ExportCommand = new(ExportAsync, () => _validation?.Passed == true);
        RestoreCommand = new(RestoreAsync, () => _demoSession is not null);

        RefreshHistory();
        Log("Pronto. O original nunca é alterado e nenhum executável é rodado automaticamente.");
    }

    public AsyncRelayCommand SelectExeCommand { get; }
    public AsyncRelayCommand AnalyzeCommand { get; }
    public AsyncRelayCommand DecompileCommand { get; }
    public AsyncRelayCommand PrepareDemoCommand { get; }
    public AsyncRelayCommand PlanCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }
    public AsyncRelayCommand BuildCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }

    public ObservableCollection<string> History { get; } = new();

    public string SelectedPath { get => _selectedPath; set => Set(ref _selectedPath, value); }
    public string ReportText { get => _reportText; set => Set(ref _reportText, value); }
    public string FeasibilityText { get => _feasibilityText; set => Set(ref _feasibilityText, value); }
    public Brush FeasibilityBrush { get => _feasibilityBrush; set => Set(ref _feasibilityBrush, value); }
    public string RequestText { get => _requestText; set => Set(ref _requestText, value); }
    public string ModelName { get => _modelName; set => Set(ref _modelName, value); }
    public bool UseAi { get => _useAi; set => Set(ref _useAi, value); }
    public string PlanText { get => _planText; set => Set(ref _planText, value); }
    public string StateText { get => _stateText; set => Set(ref _stateText, value); }
    public string LogText { get => _logText; set => Set(ref _logText, value); }

    public bool HasApiKey
    {
        get => _hasApiKey;
        set { if (Set(ref _hasApiKey, value)) OnPropertyChanged(nameof(ApiKeyStatus)); }
    }
    public string ApiKeyStatus => HasApiKey ? "Chave salva (cifrada com DPAPI)" : "Nenhuma chave salva";

    // ---------- Chaves ----------
    public void SaveApiKey(string key)
    {
        _keys.Save(ProviderKey, key);
        HasApiKey = true;
        Log("Chave do OpenRouter salva com DPAPI.");
    }

    public void RemoveApiKey()
    {
        _keys.Delete(ProviderKey);
        HasApiKey = false;
        UseAi = false;
        Log("Chave do OpenRouter removida.");
    }

    // ---------- Etapas ----------
    Task AnalyzeAsync() => Step(StudioState.Analyzing, async () =>
    {
        var path = SelectedPath;
        Log($"Analisando (somente leitura, sem executar): {path}");
        var (report, assessment, session) = await Task.Run(() =>
        {
            var r = _analyzer.Analyze(path);
            var a = _detector.Assess(r);
            var s = _workspaces.Create(path);
            return (r, a, s);
        });

        _report = report;
        _analysisSession = session;
        _logFile = Path.Combine(session.LogDir, "studio.log");
        ReportText = ReportFormatter.Format(report, assessment, session);
        FeasibilityText = $"Viabilidade: {assessment.Label} — {assessment.Summary}";
        FeasibilityBrush = assessment.Level switch
        {
            Feasibility.Limited => Frozen(0xF5, 0xB0, 0x41),
            Feasibility.AnalysisOnly => Frozen(0xE0, 0x8A, 0x3C),
            _ => Frozen(0xE5, 0x57, 0x57)
        };
        RefreshHistory();
        Log($"Análise concluída: {report.Kind}, {report.Architecture}. Backup criado no workspace.");
        return StudioState.Analyzed;
    });

    Task DecompileAsync() => Step(StudioState.Analyzing, async () =>
    {
        if (_report?.ManagedAssemblyPath is not { } asm || _analysisSession is null)
            throw new InvalidOperationException("Nenhum assembly .NET analisado.");

        if (!await _ilspy.IsAvailableAsync(CancellationToken.None))
        {
            Log("ilspycmd não encontrado. Instale (requer internet): dotnet tool install -g ilspycmd");
            return StudioState.Failed;
        }

        var outDir = Path.Combine(_analysisSession.OutputDir, "decompiled");
        Log($"Descompilando {asm} com ilspycmd...");
        var r = await _ilspy.DecompileAsync(asm, outDir, Log, CancellationToken.None);
        if (!r.Success)
        {
            Log("Descompilação FALHOU ou não gerou arquivos .cs.");
            return StudioState.Failed;
        }
        Log($"Descompilação concluída: {r.CsFiles} arquivo(s) .cs em {r.OutputDir}");
        Log("Aviso: o código descompilado raramente recompila sem ajustes manuais.");
        return StudioState.Analyzed;
    });

    Task PrepareDemoAsync() => Step(StudioState.Preparing, async () =>
    {
        var sample = DemoProject.FindSampleDir(AppContext.BaseDirectory)
            ?? throw new DirectoryNotFoundException("Projeto de teste (samples/DemoApp) não encontrado.");

        _demoSession = await Task.Run(() => _workspaces.Create(sample));
        _plan = null; _applied = false; _build = null; _validation = null; _applyLog = Array.Empty<string>();
        PlanText = "";
        _logFile = Path.Combine(_demoSession.LogDir, "studio.log");
        RefreshHistory();
        Log($"Workspace criado: {_demoSession.SessionDir}");
        return StudioState.Prepared;
    });

    Task PlanAsync() => Step(StudioState.Planning, async () =>
    {
        var s = _demoSession!;
        var files = Directory.GetFiles(s.WorkPath, "*.cs", SearchOption.AllDirectories)
            .Select(f => new SourceFile(Path.GetRelativePath(s.WorkPath, f), File.ReadAllText(f)))
            .Where(f => !Hashing.IsExcluded(f.RelativePath))
            .ToList();

        PatchPlan plan;
        if (UseAi && HasApiKey)
        {
            if (string.IsNullOrWhiteSpace(RequestText)) throw new InvalidOperationException("Descreva o pedido.");
            var list = string.Join("\n", files.Select(f => "• " + f.RelativePath));
            if (!_prompts.Confirm("Enviar arquivos à IA?",
                    $"Seu pedido e os arquivos abaixo serão enviados ao OpenRouter (modelo {ModelName}):\n\n{list}\n\nContinuar?"))
            {
                Log("Envio à IA cancelado pelo usuário.");
                return StudioState.Prepared;
            }
            var key = _keys.Load(ProviderKey) ?? throw new InvalidOperationException("Chave de API indisponível.");
            Log($"Consultando OpenRouter ({ModelName})...");
            plan = await new OpenRouterProvider(_http, key)
                .PlanAsync(new PlanRequest(RequestText, files, ModelName), CancellationToken.None);
        }
        else
        {
            Log("IA desativada: planejador manual (o texto do pedido vira a nova saudação).");
            plan = DemoProject.ManualPlan(RequestText);
        }

        _plan = plan;
        PlanText = FormatPlan(plan);
        Log("Plano gerado. Revise e clique em “Aplicar”.");
        return StudioState.Planned;
    });

    Task ApplyAsync() => Step(StudioState.Applying, async () =>
    {
        _applyLog = await Task.Run(() => _patcher.Apply(_demoSession!, _plan!));
        foreach (var l in _applyLog) Log("Alterado: " + l);
        _applied = true; _build = null; _validation = null;
        return StudioState.Applied;
    });

    Task BuildAsync() => Step(StudioState.Building, async () =>
    {
        Log("Compilando (dotnet build, Release)...");
        _build = await _builder.BuildAsync(_demoSession!, DemoProject.ProjectFile, Log, CancellationToken.None);
        _validation = null;

        if (!_build.Success)
        {
            Log($"Compilação FALHOU (exit code {_build.ExitCode}). Erros: {_build.Errors.Count}");
            foreach (var e in _build.Errors.Take(20)) Log(e);
            return StudioState.Failed;
        }

        Log("Compilação concluída. Validando o artefato...");
        _validation = ArtifactValidator.ValidateAssembly(
            Path.Combine(_build.OutputDir, DemoProject.AssemblyFile),
            _plan?.ExpectedRuntimeText, _plan?.ForbiddenRuntimeText);
        foreach (var c in _validation.Checks) Log(c);
        return _validation.Passed ? StudioState.Succeeded : StudioState.Failed;
    });

    Task ExportAsync() => Step(StudioState.Succeeded, async () =>
    {
        var folder = _prompts.PickFolder();
        if (folder is null) return StudioState.Succeeded;
        var report = new RunReport(_demoSession!, _plan, _applyLog, _build, _validation, DateTimeOffset.Now);
        var dest = await Task.Run(() => new ReleaseExporter().Export(report, folder));
        Log($"Exportado com verificação de integridade: {dest}");
        return StudioState.Succeeded;
    });

    Task RestoreAsync() => Step(StudioState.Preparing, async () =>
    {
        await Task.Run(() => _workspaces.Restore(_demoSession!));
        _applied = false; _build = null; _validation = null;
        Log("Cópia de trabalho restaurada a partir do backup (integridade verificada).");
        return StudioState.Prepared;
    });

    // ---------- Infra ----------
    async Task Step(StudioState during, Func<Task<StudioState>> body)
    {
        SetState(during);
        try { SetState(await body()); }
        catch (Exception ex)
        {
            Log("ERRO: " + ex.Message);
            SetState(StudioState.Failed);
        }
    }

    void SetState(StudioState s) => StateText = s switch
    {
        StudioState.Idle => "Pronto",
        StudioState.Analyzing => "Analisando…",
        StudioState.Analyzed => "Análise concluída",
        StudioState.Preparing => "Preparando workspace…",
        StudioState.Prepared => "Workspace pronto",
        StudioState.Planning => "Planejando…",
        StudioState.Planned => "Plano pronto para revisão",
        StudioState.Applying => "Aplicando alterações…",
        StudioState.Applied => "Alterações aplicadas (ainda não compiladas)",
        StudioState.Building => "Compilando…",
        StudioState.Succeeded => "Sucesso (compilado e validado)",
        StudioState.Failed => "Falha — veja o console",
        _ => s.ToString()
    };

    void Log(string line)
    {
        var stamped = $"[{DateTime.Now:HH:mm:ss}] {SecretRedactor.Redact(line)}";
        _ui.Post(_ =>
        {
            var t = LogText + stamped + Environment.NewLine;
            LogText = t.Length > 200_000 ? t[^150_000..] : t;
        }, null);

        var f = _logFile;
        if (f is null) return;
        try { File.AppendAllText(f, stamped + Environment.NewLine); } catch { /* log em arquivo é best-effort */ }
    }

    void RefreshHistory()
    {
        History.Clear();
        foreach (var n in _workspaces.ListSessions()) History.Add(n);
    }

    static string FormatPlan(PatchPlan p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Origem: {p.Source}");
        sb.AppendLine($"Resumo: {p.Summary}");
        foreach (var x in p.Patches)
        {
            sb.AppendLine();
            sb.AppendLine($"Arquivo: {x.File}");
            sb.AppendLine($"  - {x.Old.ReplaceLineEndings(" ")}");
            sb.AppendLine($"  + {x.New.ReplaceLineEndings(" ")}");
        }
        return sb.ToString();
    }

    static Brush Frozen(byte r, byte g, byte b)
    {
        var br = new SolidColorBrush(Color.FromRgb(r, g, b));
        br.Freeze();
        return br;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
