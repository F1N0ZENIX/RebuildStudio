# Dependências e licenças

Verifique as licenças e versões atuais antes de distribuir.

| Dependência | Uso | Licença (verificar na fonte) | Incluída no app? |
|---|---|---|---|
| .NET 8 SDK/Runtime, WPF | plataforma | MIT | requisito externo |
| System.Security.Cryptography.ProtectedData | cifrar chaves (DPAPI) | MIT | sim (NuGet) |
| System.Reflection.Metadata / PEReader | análise PE | MIT (parte do .NET) | sim (framework) |
| xunit, xunit.runner.visualstudio | testes | Apache-2.0 | só em testes |
| Microsoft.NET.Test.Sdk | testes | MIT | só em testes |
| ilspycmd (ILSpy) | descompilação .NET | MIT | **não** (instalação opcional pelo usuário) |
| OpenRouter | IA (serviço web) | termos do serviço | **não** (serviço externo, opcional) |
| Ghidra | análise nativa (planejado) | Apache-2.0 | não incluído nesta versão |
| dnSpyEx | planejado | verificar | não incluído nesta versão |

Justificativa: ILSpy entrou porque é a forma padrão e mantida de descompilar .NET por linha de comando. Ghidra e dnSpyEx ficam de fora até haver uma funcionalidade que realmente dependa deles.
