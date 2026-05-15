using System.Windows;

namespace DSCapture.Views;

public partial class LicenseErrorWindow : Window
{
    private readonly string _hwid;

    public LicenseErrorWindow(string hwid, string message)
    {
        InitializeComponent();
        _hwid = hwid;
        HwidText.Text = hwid;
        MessageText.Text = message;
        Closing += (_, _) => Application.Current.Shutdown();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_hwid);
        MessageBox.Show("기기 ID가 복사되었습니다.\n관리자에게 전달하여 라이센스를 발급받으세요.",
            "복사 완료", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
