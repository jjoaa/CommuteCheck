using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kiosk.Commands;
using Kiosk.Services.Interface;

namespace Kiosk.ViewModels;

public class SectionCommuteViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SectionCommuteViewModel> _logger;
    public ICommand RefreshCommand { get; }
    public ObservableCollection<CommuteRow> CommuteList { get; } = new();
    public ObservableCollection<UserFilter> UserFilters { get; } = new();

    public ObservableCollection<int> Years { get; } = new();
    public ObservableCollection<int> Months { get; } = new();

    private bool _filtersInitialized;
    private bool _filterAutoLoad;

    private long _hostLocationOid;
    private long _selectedUserOid;

    public long SelectedUserOid
    {
        get => _selectedUserOid;
        set
        {
            if (_selectedUserOid == value) return;
            _selectedUserOid = value;
            OnPropertyChanged();
            if (!_filterAutoLoad) _ = LoadAsync(_selectedUserOid);
        }
    }

    private int _year, _month;
    private int? _day;

    public int Year
    {
        get => _year;
        set
        {
            if (_year == value) return;
            _year = value;
            OnPropertyChanged();
            if (!_filterAutoLoad) _ = LoadAsync(_selectedUserOid);
        }
    }

    public int Month
    {
        get => _month;
        set
        {
            if (_month == value) return;
            _month = value;
            OnPropertyChanged();
            if (!_filterAutoLoad) _ = LoadAsync(_selectedUserOid);
        }
    }

    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public SectionCommuteViewModel(ISettingsService settings, ILogger<SectionCommuteViewModel> logger)
    {
        _settings = settings;
        _logger = logger;

        var now = DateTime.Now; // default는 오늘 기준
        for (int y = now.Year - 2; y <= now.Year + 1; y++) Years.Add(y);
        for (int m = 1; m <= 12; m++) Months.Add(m);
        _year = now.Year;
        _month = now.Month;
        _day = null;

        UserFilters.Add(new UserFilter { UserOid = 0, Name = "전체" });
        _selectedUserOid = 0;

        RefreshCommand = new RelayCommand(
            () => ResetFilters(reload: true),
            () => !IsBusy && _hostLocationOid > 0
        );
    }

    public void SetContext(long hostLocationOid, int pageNo) => _hostLocationOid = hostLocationOid;

    public async Task LoadAsync(long userOid = 0)
    {
        if (_hostLocationOid <= 0) return;

        try
        {
            IsBusy = true;
            await EnsureUserFiltersAsync();

            var list = await _settings.GetDailyCommuteAsync(_hostLocationOid, Year, Month, _day, userOid);
            CommuteList.Clear();
            foreach (var r in list)
            {
                CommuteList.Add(new CommuteRow
                {
                    WorkDate = r.WorkDate,
                    UserName = r.UserName,
                    CheckIn = r.CheckInTime,
                    CheckOut = r.CheckOutTime,
                    Status = r.Status
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CommuteVM] LoadAsync failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // 필터 초기화 메서드
    public void ResetFilters(bool reload = true)
    {
        var now = DateTime.Now;

        _filterAutoLoad = true;
        Year = now.Year;
        Month = now.Month;
        _day = null;
        SelectedUserOid = 0; // "전체"
        _filterAutoLoad = false;

        if (reload) _ = LoadAsync(SelectedUserOid);
    }

    public sealed class UserFilter
    {
        public long UserOid { get; init; }
        public string Name { get; init; } = "";
    }

    // 직원 목록 로드
    private async Task EnsureUserFiltersAsync()
    {
        if (_filtersInitialized || _hostLocationOid <= 0) return;

        var page = await _settings.GetUserListAsync(_hostLocationOid, 1);
        UserFilters.Clear();
        UserFilters.Add(new UserFilter { UserOid = 0, Name = "전체" });
        foreach (var u in page.Users)
            UserFilters.Add(new UserFilter { UserOid = u.UserOid, Name = u.UserName });

        _filtersInitialized = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class CommuteRow
{
    public DateTime WorkDate { get; init; }
    public string UserName { get; init; } = "";
    public DateTime? CheckIn { get; init; }
    public DateTime? CheckOut { get; init; }
    public string Status { get; init; } = "";
}