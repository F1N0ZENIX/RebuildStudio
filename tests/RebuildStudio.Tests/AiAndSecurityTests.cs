using RebuildStudio.Core.Ai;
using RebuildStudio.Core.Security;

namespace RebuildStudio.Tests;

public class AiAndSecurityTests
{
    [Fact]
    public void PlanParser_le_json_dentro_de_cerca_de_codigo()
    {
        const string raw = "Claro!\n```json\n{\"summary\":\"troca\",\"patches\":[{\"file\":\"Program.cs\",\"old\":\"a\",\"new\":\"b\"}]}\n```";
        var plan = PlanParser.Parse(raw, "teste");
        Assert.Equal("troca", plan.Summary);
        Assert.Single(plan.Patches);
        Assert.Equal("Program.cs", plan.Patches[0].File);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem json aqui")]
    [InlineData("{\"summary\":\"x\",\"patches\":[]}")]
    [InlineData("{\"patches\":[{\"file\":\"a.cs\",\"old\":\"\",\"new\":\"b\"}]}")]
    public void PlanParser_rejeita_respostas_invalidas(string raw)
    {
        Assert.Throws<FormatException>(() => PlanParser.Parse(raw, "teste"));
    }

    [Fact]
    public void Redactor_oculta_chaves()
    {
        var r = SecretRedactor.Redact("Authorization: Bearer sk-or-v1-abcdef1234567890 e api_key=ZZZZ99998888");
        Assert.DoesNotContain("abcdef1234567890", r);
        Assert.DoesNotContain("ZZZZ99998888", r);
    }

    [Fact]
    public void Redactor_nao_altera_texto_comum() =>
        Assert.Equal("compilação concluída", SecretRedactor.Redact("compilação concluída"));
}
