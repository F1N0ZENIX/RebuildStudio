using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RebuildStudio.Core.Security;
using RebuildStudio.Core.Workspace;

namespace RebuildStudio.Core.Ai;

public sealed class OpenRouterProvider : IAiProvider
{
    const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";

    const string SystemPrompt =
        "Você é um planejador de modificações de código. Responda SOMENTE com JSON no formato " +
        "{\"summary\": string, \"patches\": [{\"file\": caminho relativo, \"old\": trecho exato que aparece UMA vez no arquivo, \"new\": substituição}]}. " +
        "Altere apenas os arquivos fornecidos. Nunca altere arquivos de projeto/build, nunca adicione comandos, scripts, " +
        "acesso à rede nem código que baixe ou execute programas. Faça mudanças mínimas.";

    readonly HttpClient _http;
    readonly string _apiKey;

    public OpenRouterProvider(HttpClient http, string apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public string Name => "OpenRouter";

    public async Task<PatchPlan> PlanAsync(PlanRequest request, CancellationToken ct)
    {
        var user = new StringBuilder();
        user.AppendLine("Pedido do usuário: " + request.UserRequest);
        foreach (var f in request.Files)
        {
            user.AppendLine();
            user.AppendLine($"=== {f.RelativePath} ===");
            user.AppendLine(f.Content);
        }

        var body = new
        {
            model = request.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = user.ToString() }
            }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await _http.SendAsync(msg, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var shown = text.Length > 400 ? text[..400] : text;
            throw new AiProviderException($"OpenRouter retornou {(int)resp.StatusCode}: {SecretRedactor.Redact(shown)}");
        }

        using var doc = JsonDocument.Parse(text);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return PlanParser.Parse(content, $"IA: OpenRouter/{request.Model}");
    }
}
