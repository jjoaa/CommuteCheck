using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Kiosk.Commands;
using Kiosk.ViewModels;
using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;

namespace Kiosk.Pages
{
    public partial class FaceRecognitionPage : Page
    {
        private readonly INavigationService _navigationService;
        private readonly ISessionService _sessionService;
        private readonly ILogger<FaceRecognitionPage> _logger;

        public FaceRecognitionPage(VmDeps<FaceRecognitionPage> deps)
        {
            InitializeComponent();

            _logger = deps.Logger;
            _sessionService = deps.Session;
            _navigationService = deps.Nav;

            this.Loaded += FaceRecognitionPage_Loaded;
            this.LayoutUpdated += FaceRecognitionPage_LayoutUpdated;
        }

        /*UI 마스킹*/
        private bool _maskingApplied = false;

        private void FaceRecognitionPage_LayoutUpdated(object sender, EventArgs e)
        {
            if (!_maskingApplied && RootGrid.ActualWidth > 0 && RootGrid.ActualHeight > 0)
            {
                UpdateMaskingGeometry();
                _maskingApplied = true;
                Console.WriteLine("[Masking] 마스킹 적용됨");
            }
        }

        private async void FaceRecognitionPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is FaceRecognitionViewModel vm)
            {
                try
                {
                    await vm.StartRecognitionAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FaceRecognitionPage] 카메라 시작 실패: {ex.Message}");
                }
            }
        }

        private void UpdateMaskingGeometry()
        {
            if (RootGrid == null || MaskingPath == null)
                return;

            double width = RootGrid.ActualWidth;
            double height = RootGrid.ActualHeight;

            // Console.WriteLine($"[UpdateMaskingGeometry] size: {width} x {height}");
            if (width <= 0 || height <= 0)
            {
                Console.WriteLine("[UpdateMaskingGeometry] 화면 사이즈가 유효하지 않음. 마스킹 생략.");
                return;
            }

            double radius = 280;
            double centerX = width / 2;
            double centerY = height / 2;

            var background = new RectangleGeometry(new Rect(0, 0, width, height));
            var transparentCircle = new EllipseGeometry(new Point(centerX, centerY), radius, radius);

            var geometry = new CombinedGeometry
            {
                GeometryCombineMode = GeometryCombineMode.Exclude,
                Geometry1 = background,
                Geometry2 = transparentCircle
            };

            MaskingPath.Data = geometry;
            //  Console.WriteLine($"[UpdateMaskingGeometry] 마스킹 적용 완료 (중심: {centerX}, {centerY}, 반지름: {radius})");
        }

        /*버튼-핸드폰*/
        private async void OnPhoneClick(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[FaceRecognitionPage] 폰 화면 이동");
            if (DataContext is FaceRecognitionViewModel viewModel)
            {
                try
                {
                    await viewModel.StopRecognitionAsync();

                    var session = _sessionService.GetSession();
                    if (session == null || string.IsNullOrEmpty(session.SessionId))
                    {
                        MessageBox.Show("세션이 만료되었습니다. 다시 로그인해주세요.");
                        _navigationService.NavigateTo<LoginViewModel>();
                        return;
                    }

                    _navigationService.NavigateTo<CheckPhoneViewModel>();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"폰 화면 이동 중 오류 발생: {ex.Message}");
                }
            }
        }

        /*버튼-지도*/
        private async void OnMapClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is FaceRecognitionViewModel viewModel)
            {
                try
                {
                    await viewModel.StopRecognitionAsync();

                    var session = _sessionService.GetSession();
                    if (session == null || string.IsNullOrEmpty(session.SessionId))
                    {
                        MessageBox.Show("세션이 만료되었습니다. 다시 로그인해주세요.");
                        _navigationService.NavigateTo<LoginViewModel>();
                        return;
                    }

                    _navigationService.NavigateTo<LocationSelectionViewModel>();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"지도 화면 이동 중 오류 발생: {ex.Message}");
                }
            }
        }
    }
}