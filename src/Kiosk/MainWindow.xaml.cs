using System;
using System.Windows;
using Kiosk.Services;
using Kiosk.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Kiosk.Pages; 

namespace Kiosk
{
    public partial class MainWindow : Window
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MainWindow> _logger;

        public MainWindow(
        IServiceProvider serviceProvider,
        ILogger<MainWindow> logger)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _logger = logger;

            // 초기 페이지로 LoginPage 표시
            var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
            MainFrame.Navigate(loginPage);

            Loaded += Window_Loaded;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
#if DEBUG
                // 노트북 세로모드 테스트용 크기
                double aspectRatio = 2048.0 / 1536.0; // 원래 가로/세로 비율
                double fixedHeight = 1020;             // 원하는 세로 길이 기준
                double calculatedWidth = fixedHeight / aspectRatio; // 세로모드니까 세로 ÷ 비율

                this.Width = calculatedWidth;  // 가로가 세로보다 작음
                this.Height = fixedHeight;

                this.Left = 0;
                this.Top = 0;
#else
        // 테블릿 세로모드
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        int screenWidth = screen.Bounds.Width;
        int screenHeight = screen.Bounds.Height;

        var source = PresentationSource.FromVisual(this);
        double dpiX = 96.0, dpiY = 96.0;
        if (source?.CompositionTarget != null)
        {
            dpiX *= source.CompositionTarget.TransformToDevice.M11;
            dpiY *= source.CompositionTarget.TransformToDevice.M22;
        }

        // 세로 모드 가정: 높이가 화면의 더 긴 쪽으로 설정되었는지 확인 필요
        // 보통 세로모드면 세로가 더 길기 때문에 heightDip > widthDip 인지 체크
        double widthDip = screenWidth * (96.0 / dpiX);
        double heightDip = screenHeight * (96.0 / dpiY);

        // 만약 현재 해상도가 가로가 더 크면(가로모드), 세로모드로 강제 전환 (swap)
        if(widthDip > heightDip)
        {
            var temp = widthDip;
            widthDip = heightDip;
            heightDip = temp;
        }
        // 여기서 세로 크기 살짝 줄이기
        heightDip -= 60;  // 원하는 만큼 줄이기 (예: 60)

        this.Width = widthDip;
        this.Height = heightDip;
        this.Left = 0;
        this.Top = 0;
#endif
            }
            catch (Exception ex)
            {
                _logger.LogError($"화면 크기 조절 실패: {ex.Message}");
            }
        }
    }
}
