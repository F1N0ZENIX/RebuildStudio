using System.Windows;
using System.Windows.Controls;

namespace RebuildStudio.App;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(new WpfPrompts());
        DataContext = _vm;
    }

    void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();

    void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(KeyBox.Password)) return;
        _vm.SaveApiKey(KeyBox.Password);
        KeyBox.Clear();
    }

    void RemoveKey_Click(object sender, RoutedEventArgs e) => _vm.RemoveApiKey();
}
