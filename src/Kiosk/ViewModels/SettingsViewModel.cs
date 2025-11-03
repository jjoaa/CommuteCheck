using Kiosk.Commands;
using Kiosk.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kiosk.Models;
using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;

namespace Kiosk.ViewModels;

public enum SettingsSection
{
    Staff,
    Commute
}

public class SettingsViewModel : INotifyPropertyChanged, INavigationAware
{
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<SettingsViewModel> _logger;
    public string UserName { get; set; }

    private object _currentSectionVM;

    public object CurrentSectionVM
    {
        get => _currentSectionVM;
        set
        {
            if (_currentSectionVM == value) return;
            _currentSectionVM = value;
            OnPropertyChanged();
        }
    }

    private SettingsSection _currentSection;

    public SettingsSection CurrentSection
    {
        get => _currentSection;
        set
        {
            if (_currentSection == value) return;
            _currentSection = value;
            OnPropertyChanged();
        }
    }

    private bool _isSidebarOpen;

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen == value) return;
            _isSidebarOpen = value;
            OnPropertyChanged();
        }
    }

    public ICommand ToggleSidebarCommand { get; }
    public ICommand CloseSidebarCommand { get; }
    public ICommand Open_StaffPage { get; }
    public ICommand Open_CommuteHistoryPage { get; }
    public ICommand ReturnFaceRecognitionCommand { get; }

    private readonly SectionStaffViewModel _staffVM;
    private readonly SectionCommuteViewModel _commuteVM;

    public SettingsViewModel(
        VmDeps<SettingsViewModel> deps,
        SectionStaffViewModel staffVM,
        SectionCommuteViewModel commuteVM)
    {
        _navigationService = deps.Nav;
        _sessionService = deps.Session;
        _logger = deps.Logger;

        _staffVM = staffVM;
        _commuteVM = commuteVM;

        ReturnFaceRecognitionCommand = new AsyncRelayCommand(async () =>
        {
            var s = _sessionService.GetValidatedSession();

            var model = new FaceRecognitionModel
            {
                HostLocationOid = s.HostLocationOid ?? 0,
                LocationName = s.LocationName ?? string.Empty,
                StatusText = "화면에 얼굴을 위치시켜주세요",
                StatusAutoRevertSec = 0,
                CurrentDateTime = DateTime.Now
            };

            _navigationService.NavigateTo<FaceRecognitionViewModel>(model);
        });

        Open_StaffPage = new RelayCommand(() =>
        {
            CurrentSectionVM = _staffVM;
            CurrentSection = SettingsSection.Staff;
            IsSidebarOpen = false;
        });

        Open_CommuteHistoryPage = new RelayCommand(() =>
        {
            _commuteVM.ResetFilters(reload: true);

            CurrentSectionVM = _commuteVM;
            CurrentSection = SettingsSection.Commute;
            IsSidebarOpen = false;
        });
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
        CloseSidebarCommand = new RelayCommand(() => IsSidebarOpen = false);
        // 기본 섹션 = 직원
        CurrentSectionVM = _staffVM;
        CurrentSection = SettingsSection.Staff;
    }

    public void OnNavigatedTo(object? parameter)
    {
        CurrentSectionVM = _staffVM;
        CurrentSection = SettingsSection.Staff;

        if (parameter is SettingsModel.InitSettingsModel p)
        {
            UserName = p.UserName ?? "";

            _staffVM.SetContext(p.HostLocationOid, p.PageNo);
            _commuteVM.SetContext(p.HostLocationOid, p.PageNo);

            _ = _staffVM.LoadAsync();
            _ = _commuteVM.LoadAsync();
        }
        else
        {
            // fallback - parameter가 없을 때도 안전하게 세션에서 가져오기
            var s = _sessionService.GetValidatedSession();
            _staffVM.SetContext(s.HostLocationOid ?? 0, 1);
            _commuteVM.SetContext(s.HostLocationOid ?? 0, 1);
            _ = _staffVM.LoadAsync();
            _ = _commuteVM.LoadAsync();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}