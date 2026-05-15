using System.Windows;
using System.Windows.Input;

namespace DSCapture.Views;

public partial class TextInputDialog : Window
{
    public string InputText => InputBox.Text;

    public TextInputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus();
    }

    private void OnOk(object s, RoutedEventArgs e) => DialogResult = true;
    private void OnCancel(object s, RoutedEventArgs e) => DialogResult = false;
    private void OnKeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Return) { DialogResult = true; e.Handled = true; }
    }
}
