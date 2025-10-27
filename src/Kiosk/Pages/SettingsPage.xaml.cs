using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Kiosk.Pages;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    // 사이드바 열기
    private void Side_Open(object sender, RoutedEventArgs e)
    {
        BackdropHost.IsHitTestVisible = true; // 딤 클릭 가능
        var sb = (Storyboard)FindResource("OpenSidebarSB");
        sb.Begin();
    }

    // 사이드바 닫기 (X 버튼)
    private void Side_Close(object sender, RoutedEventArgs e)
    {
        BackdropHost.IsHitTestVisible = false; // 딤 클릭 금지
        var sb = (Storyboard)FindResource("CloseSidebarSB");
        sb.Begin();
    }

    // 딤 클릭 → 닫기
    private void BackdropHost_Mouse(object s, MouseButtonEventArgs e)
    {
        Side_Close(s, e);
    }
}