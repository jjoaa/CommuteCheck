using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Kiosk.ViewModels;
using Kiosk.Pages;
using Kiosk.Services.Interface;

namespace Kiosk.Services
{
    public interface INavigationAware
    {
        void OnNavigatedTo(object? parameter);
    }

    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _sp;
        private readonly Dictionary<Type, Type> _map = new();

        public NavigationService(IServiceProvider sp)
        {
            _sp = sp;
            _map[typeof(LocationSelectionViewModel)] = typeof(LocationSelectionPage);
            _map[typeof(FaceRecognitionViewModel)] = typeof(FaceRecognitionPage);
            _map[typeof(CheckPhoneViewModel)] = typeof(CheckPhonePage);
            _map[typeof(CommuteCheckViewModel)] = typeof(CommuteCheckPage);
            _map[typeof(SettingsViewModel)] = typeof(SettingsPage);
        }

        public void NavigateTo<TViewModel>(object? parameter = null)
        {
            if (!_map.TryGetValue(typeof(TViewModel), out var pageType))
                throw new InvalidOperationException($"No mapping for {typeof(TViewModel).Name}");

            // 1) DI에서 VM/페이지를 '등록된 수명'으로 받아오기
            var vm = _sp.GetRequiredService<TViewModel>();
            var page = (Page)_sp.GetRequiredService(pageType);

            // 2) 파라미터 주입 (Model set or INavigationAware)
            if (parameter != null)
            {
                // a) 프로퍼티 이름 "Model" 지원
                var prop = typeof(TViewModel).GetProperty("Model");
                if (prop?.CanWrite == true) prop.SetValue(vm, parameter);

                // b) 콜백 인터페이스 지원(있으면 호출)
                if (vm is INavigationAware aware) aware.OnNavigatedTo(parameter);
            }

            // 3) DataContext 설정 후 이동
            page.DataContext = vm;

            if (Application.Current.MainWindow is MainWindow mw && mw.MainFrame != null)
            {
                mw.MainFrame.Navigate(page);
                return;
            }

            throw new InvalidOperationException("Frame을 찾을 수 없습니다. MainWindow에 MainFrame이 정의되어 있는지 확인하세요.");
        }
    }
}