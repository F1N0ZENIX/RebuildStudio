# Como compilar no GitHub (sem terminal)

Quem compila é o **GitHub Actions** (um servidor Windows do GitHub), não o ChatGPT. O ChatGPT só serve para ajudar a interpretar erros.

1. Crie uma conta em github.com e um repositório novo (ex.: `rebuild-studio-ai`), vazio.
2. Extraia o `.zip` e, no repositório, clique em **Add file → Upload files**. Arraste **todo o conteúdo** da pasta (incluindo a pasta oculta `.github`) e clique em **Commit changes**.
   - Se a pasta `.github` não subir pelo navegador, use o **GitHub Desktop** (gratuito) para enviar o projeto.
3. Abra a aba **Actions**. O workflow **CI** começa sozinho (leva alguns minutos). Se pedir, clique em *I understand my workflows, enable them*.
4. Quando ficar verde ✅, abra a execução e baixe, em **Artifacts**, `RebuildStudioAI-win-x64`.
5. Se ficar vermelho ❌: abra a execução → clique na etapa que falhou → copie o trecho do erro e cole numa conversa (comigo ou no ChatGPT) pedindo a correção. Os resultados dos testes ficam em `test-results`.
6. Para criar um release: na aba **Releases**, crie uma tag `v0.1.0`; o workflow anexa o `.zip` automaticamente.
