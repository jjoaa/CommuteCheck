using System.Windows.Controls;
using Kiosk.ViewModels;

namespace Kiosk.Pages;

public partial class CommuteCheckPage : Page
{
    public CommuteCheckPage(CommuteCheckViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}