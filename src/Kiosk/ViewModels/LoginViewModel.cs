using Kiosk.Commands;
using Kiosk.Pages;
using Kiosk.Services;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;

namespace Kiosk.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private readonly LoginService _loginService;
        private readonly INavigationService _navigationService;
        private readonly ILogger<LoginViewModel> _logger;

        private string _username = string.Empty;

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged();
                    CheckEnableLogin();
                }
            }
        }

        private string _password = string.Empty;

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged();
                    CheckEnableLogin();
                }
            }
        }

        private string _errorMessage = string.Empty;

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isLoggingIn = false;
        private bool _isLoginEnabled = false;

        public bool IsLoginEnabled
        {
            get => _isLoginEnabled;
            set
            {
                if (_isLoginEnabled != value)
                {
                    _isLoginEnabled = value;
                    OnPropertyChanged();
                    (LoginCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncCommand LoginCommand { get; }

        public LoginViewModel(LoginService loginService, VmDeps<LoginViewModel> deps)
        {
            _loginService = loginService;
            _logger = deps.Logger;
            _navigationService = deps.Nav;

            LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, CanExecuteLogin);
        }

        private bool CanExecuteLogin() => IsLoginEnabled && !_isLoggingIn;

        private async Task ExecuteLoginAsync()
        {
            if (_isLoggingIn) return;

            try
            {
                _isLoggingIn = true;
                (LoginCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
                ErrorMessage = string.Empty;

                var deviceId = PreferenceUtils.GetString("DeviceId");
                var response = await _loginService.HostSignInAsync(Username, Password);

                if (response != null)
                {
                    _logger.LogInformation($"로그인 성공: {Username}");
                    _navigationService.NavigateTo<LocationSelectionViewModel>();
                }
                else
                {
                    ErrorMessage = "로그인 실패. 아이디/비밀번호를 확인하세요.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"로그인 중 오류 발생: {ex.Message}");
                ErrorMessage = "로그인 중 오류가 발생했습니다.";
            }
            finally
            {
                _isLoggingIn = false;
                (LoginCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
            }
        }

        private void CheckEnableLogin()
        {
            IsLoginEnabled = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}