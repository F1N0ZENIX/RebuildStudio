using System.Windows;
using Microsoft.Win32;

namespace RebuildStudio.App;

public interface IUserPrompts
{
    bool Confirm(string title, string message);
    string? PickExecutable();
    string? PickFolder();
}

public sealed class WpfPrompts : IUserPrompts
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? PickExecutable()
    {
        var d = new OpenFileDialog { Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos|*.*", Title = "Selecione um .exe" };
        return d.ShowDialog() == true ? d.FileName : null;
    }

    public string? PickFolder()
    {
        var d = new OpenFolderDialog { Title = "Escolha a pasta de exportação" };
        return d.ShowDialog() == true ? d.FolderName : null;
    }
}
