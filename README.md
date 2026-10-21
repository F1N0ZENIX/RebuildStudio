# Rebuild Studio AI

Aplicativo desktop (WPF / .NET 8) para Windows 11 que **analisa executáveis**, planeja modificações com IA (opcional) e **compila e valida** o resultado quando existe uma estratégia tecnicamente viável.

> **Não há promessa de compatibilidade universal.** Recompilar um EXE arbitrário geralmente não é possível. O app informa claramente o que pode e o que não pode ser feito com cada arquivo.

## Estado da versão 0.1.0 (MVP)

| Recurso | Estado |
|---|---|
| Janela WPF com tema escuro, console de logs, histórico | ✅ implementado |
| Seleção de EXE e análise PE (arquitetura, seções, hash, SHA-256) | ✅ implementado |
| Detecção de .NET (incl. apphost), Electron e binário nativo | ✅ implementado |
| Indicador de viabilidade (o que é/não é possível) | ✅ implementado |
| Workspace isolado: backup somente leitura + cópia de trabalho + restauração com verificação de hash | ✅ implementado |
| Descompilação com ILSpy (`ilspycmd`) | ✅ integrado, **ferramenta externa opcional** |
| Plano de alterações com IA (OpenRouter) ou planejador manual | ✅ implementado |
| Aplicar patch → compilar (MSBuild/.NET SDK) → validar → exportar + relatório | ✅ demonstrado com o **projeto de teste** `samples/DemoApp` |
| Testes automatizados (xUnit) e CI (GitHub Actions) | ✅ implementado |
| Edição de recursos PE (ícones, textos) | ⏳ planejado, **não implementado** |
| Edição/reempacotamento de Electron (`app.asar`) | ⏳ planejado, **não implementado** |
| Análise de binários nativos com Ghidra | ⏳ planejado, **não implementado** |
| dnSpyEx | ❌ não incluído nesta versão (sem necessidade comprovada ainda) |
| Comparação visual (diff) lado a lado | ⏳ parcial: o plano mostra `- antes / + depois` |

O que o fluxo de demonstração faz de verdade: copia `samples/DemoApp` para um workspace, troca a saudação do programa (por pedido manual ou via IA), compila com `dotnet build`, confere que o `DemoApp.dll` foi produzido **e** contém o novo texto (e não o antigo), e exporta artefatos + `relatorio.md`. Falhas de compilação aparecem como falha, nunca como sucesso.

## Requisitos

- Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (para compilar o app **e** para o app compilar projetos)
- Opcional: `dotnet tool install -g ilspycmd` (descompilação)
- Opcional: chave do [OpenRouter](https://openrouter.ai) (IA)

## Instalação

**Pelo release do GitHub:** baixe `RebuildStudioAI-win-x64.zip`, extraia e execute `RebuildStudioAI.exe` (requer o .NET 8 Desktop Runtime).

**Pelo código-fonte:**
```powershell
dotnet build src/RebuildStudio.App -c Release
dotnet run --project src/RebuildStudio.App
```

## Uso

1. **Analisar um EXE:** *Selecionar EXE…* → *Analisar*. O arquivo **nunca é executado**. Um backup é criado no workspace.
2. **Fluxo de modificação (projeto de teste):** *Preparar workspace* → escreva o pedido → *Planejar* → revise o plano → *Aplicar* → *Compilar e validar* → *Exportar*.
3. **IA (opcional):** cole a chave do OpenRouter e clique em *Salvar chave* (cifrada com DPAPI, só para o seu usuário do Windows). Marque *Usar IA*. Antes de enviar, o app lista os arquivos que serão transmitidos e pede confirmação.
4. **Restaurar original** volta a cópia de trabalho ao estado do backup.

Dados ficam em `%LOCALAPPDATA%\RebuildStudioAI` (`workspaces`, `keys`). Cada sessão tem `logs\studio.log`.

## Segurança

- Executáveis desconhecidos nunca são executados; a análise é estática.
- O original fica intacto; alterações só ocorrem em cópias dentro do workspace.
- Caminhos são validados (nada fora do workspace). Patches só alteram extensões permitidas (`.cs`, `.txt`, `.json`, `.resx`, `.md`, `.xaml`, `.html`, `.css`, `.js`) — **arquivos de build (`.csproj`, `.props`, `.targets`) são bloqueados**, porque poderiam executar comandos na compilação.
- A IA só devolve patches de texto (JSON) que você revisa; o app não executa comandos vindos da IA.
- Chaves de API: DPAPI, nunca no código, nos logs (há redação automática) ou nos releases.
- Cada patch é atômico (tudo ou nada) e o trecho original precisa existir exatamente uma vez.
- **Limite honesto:** o `dotnet build` roda com as permissões do seu usuário. Há timeout e argumentos sem shell, mas **não é um sandbox**. Compile apenas projetos em que você confia. Isolamento forte (contêiner/Windows Sandbox) é trabalho futuro.
- O app não tenta contornar licenças, DRM, autenticação ou proteções de terceiros. Não use em software sem autorização.

## Estrutura

```
src/RebuildStudio.Core   lógica: análise PE, capacidades, workspace, patches, build, IA, exportação
src/RebuildStudio.App    interface WPF (MVVM)
tests/RebuildStudio.Tests  testes xUnit (incluem um fluxo real com dotnet build)
samples/DemoApp          projeto .NET de teste controlado
.github/workflows/ci.yml compila, testa, publica artefato e cria release em tags v*
```

## Desenvolvimento

```powershell
dotnet test tests/RebuildStudio.Tests -c Release
```

Versionamento semântico: a versão fica em `Directory.Build.props`. Para publicar um release: atualize a versão, faça commit e crie uma tag `v0.1.0`; o workflow anexa o `.zip` ao release.

## Licença

Defina a licença do projeto antes de publicar (nenhuma foi escolhida). Veja `DEPENDENCIES.md` para as dependências.
