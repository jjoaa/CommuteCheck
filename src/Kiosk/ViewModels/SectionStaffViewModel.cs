using Kiosk.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Kiosk.Models;
using Microsoft.Extensions.Logging;
using Kiosk.Services.Interface;
using Kiosk.Utils;

namespace Kiosk.ViewModels;

public enum AssignmentKind
{
    Role,
    Department
}

public class SectionStaffViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SectionStaffViewModel> _logger;

    public ObservableCollection<UserRow> UsersList { get; } = new();
    public AssignmentKind CurrentAssignmentKind { get; set; } = AssignmentKind.Role;
    public ObservableCollection<Assignment> RoleOptions { get; } = new(); // 직무
    public ObservableCollection<Assignment> DeptOptions { get; } = new(); // 부서 -> 추후

    private bool _isLivenessOn;

    public bool IsLivenessOn
    {
        get => _isLivenessOn;
        set
        {
            _isLivenessOn = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand Invite_StaffTemporary { get; }
    public ICommand Invite_StaffRegular { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand SetAdminCommand { get; }
    public ICommand SetGeneralCommand { get; }

    private long _hostLocationOid;
    private int _pageNo = 1;
    private bool _pressLevelSync;

    private bool _busy;

    public bool IsBusy
    {
        get => _busy;
        private set
        {
            _busy = value;
            OnPropertyChanged();
            (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public SectionStaffViewModel(ISettingsService settings, ILogger<SectionStaffViewModel> logger)
    {
        _settings = settings;
        _logger = logger;

        RefreshCommand = new RelayCommand(async () => await LoadAsync(), () => !IsBusy);
        Invite_StaffTemporary = new RelayCommand(AddTemp);
        Invite_StaffRegular = new RelayCommand(AddRegular);
        DeleteUserCommand = new RelayCommand<UserRow>(async row => await DeleteUserAsync(row));

        SetAdminCommand = new RelayCommand<UserRow>(async row => await UpdateLevelAsync(row, isAdmin: true));
        SetGeneralCommand = new RelayCommand<UserRow>(async row => await UpdateLevelAsync(row, isAdmin: false));
    }

    public void SetContext(long hostLocationOid, int pageNo)
    {
        _hostLocationOid = hostLocationOid;
        _pageNo = pageNo <= 0 ? 1 : pageNo;
        (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task LoadAsync()
    {
        if (_hostLocationOid <= 0) return;

        try
        {
            IsBusy = true;
            _pressLevelSync = true;
            _logger.LogInformation("[StaffVM] Load users: oid={Oid}, page={Page}", _hostLocationOid, _pageNo);

            // 1) 서버에서 직원 목록 가져오기
            var page = await _settings.GetUserListAsync(_hostLocationOid, _pageNo, CancellationToken.None);
            var list = page.Users ?? new List<SettingsModel.StaffListModel>();

            // 2) 기존 구독 해제
            foreach (var r in UsersList)
                r.PropertyChanged -= OnRowPropertyChanged;

            // 3) UsersList 채우기 
            UsersList.Clear();
            foreach (var u in list)
            {
                string? assignmentCode =
                    CurrentAssignmentKind == AssignmentKind.Role
                        ? (u.Role ?? "")
                        : (u.Department ?? ""); // Department 필드가 생기면 교체

                var row = new UserRow
                {
                    UserOid = u.UserOid,
                    UserName = u.UserName,
                    AssignmentCode = string.IsNullOrWhiteSpace(assignmentCode) ? null : assignmentCode,
                    IsAdmin = u.Level == "5", // || u.Level == "9",
                    IsGeneral = string.IsNullOrEmpty(u.Level) || u.Level == "0",
                };
                row.PropertyChanged += OnRowPropertyChanged;
                UsersList.Add(row);
            }

            RoleOptions.Clear();
            DeptOptions.Clear();

            var roleNames = list.Select(x => x.Role)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s);
            foreach (var name in roleNames)
                RoleOptions.Add(new Assignment { Id = name!, Name = name! });

            var deptNames = list.Select(x => x.Department) // 서버 모델에 Department 생기면 활성화
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .OrderBy(s => s);
            foreach (var name in deptNames)
                DeptOptions.Add(new Assignment { Id = name!, Name = name! });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffVM] LoadAsync failed");
        }
        finally
        {
            _pressLevelSync = false;
            IsBusy = false;
        }
    }

    /* 직원 추가 로직 */
    public async void AddRegular()
    {
        if (_hostLocationOid <= 0) return;

        var owner = Application.Current.MainWindow;
        var dialog = new Kiosk.Views.PopupInviteStaff();

        var result = BackdropHelper.ShowDialogWithBackdrop(owner, dialog);
        if (result == true)
        {
            var vm = (Kiosk.ViewModels.PopupInviteStaffViewModel)dialog.DataContext;
            var phone = vm.PhoneNumber?.Trim();
            if (string.IsNullOrEmpty(phone)) return;

            try
            {
                IsBusy = true;
                //_logger.LogInformation("[StaffVM] Invite: {Phone}", phone);

                var ok = await _settings.Invite_StaffRegularAsync(_hostLocationOid, phone, CancellationToken.None);
                if (ok)
                {
                    MessageBox.Show("초대가 완료되었습니다.", "직원 초대",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadAsync();
                }
                else
                {
                    MessageBox.Show("초대에 실패했습니다. 번호를 확인하거나 잠시 후 다시 시도하세요.",
                        "직원 초대 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StaffVM] Invite error");
                MessageBox.Show("오류가 발생했습니다.", "직원 초대 오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    /* TODO 임시직원 추가 로직 */
    private void AddTemp()
    {
    }

    /*새로고침*/
    private void Refresh()
    {
        _ = LoadAsync();
    }

    /* 삭제 */
    private async Task DeleteUserAsync(UserRow? row)
    {
        if (row is null || _hostLocationOid <= 0) return;

        try
        {
            IsBusy = true;
            var ok = await _settings.DeleteUserAsync(_hostLocationOid, row.UserOid, CancellationToken.None);
            if (ok)
            {
                UsersList.Remove(row);
                //_logger.LogInformation("[StaffVM] Deleted user {User} ({Oid})", row.UserName, row.UserOid);
            }
            else
            {
                _logger.LogWarning("[StaffVM] DeleteUserAsync failed for {User} ({Oid})", row.UserName, row.UserOid);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffVM] DeleteUserAsync error for {User} ({Oid})", row?.UserName, row?.UserOid);
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /* 권한 변경 (level) */
    private async Task UpdateLevelAsync(UserRow? row, bool isAdmin)
    {
        if (row == null || _hostLocationOid <= 0) return;

        try
        {
            IsBusy = true;
            var level = isAdmin ? "5" : "0";
            var ok = await _settings.UpdateUserLevelAsync(_hostLocationOid, row.UserOid, level, CancellationToken.None);
            if (ok)
            {
                _pressLevelSync = true;
                row.IsAdmin = isAdmin;
                row.IsGeneral = !isAdmin;
                _pressLevelSync = false;

                _logger.LogInformation("[StaffVM] Level updated: user={User} -> {Level}", row.UserName, level);
            }
            else
            {
                _logger.LogWarning("[StaffVM] UpdateUserLevelAsync failed for {User}", row.UserName);
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffVM] UpdateLevelAsync error");
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_pressLevelSync) return;
        if (sender is not UserRow row) return;
        if (e.PropertyName != nameof(UserRow.IsAdmin)) return;

        var desiredLevel = row.IsAdmin ? "5" : "0";
        try
        {
            IsBusy = true;
            var ok = await _settings.UpdateUserLevelAsync(_hostLocationOid, row.UserOid, desiredLevel,
                CancellationToken.None);
            if (!ok)
            {
                _logger.LogWarning("[StaffVM] UpdateUserLevel failed: {User} -> {Level}", row.UserName, desiredLevel);
                await LoadAsync();
            }
            else
            {
                _logger.LogInformation("[StaffVM] Level updated: {User} -> {Level}", row.UserName, desiredLevel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StaffVM] OnRowPropertyChanged update failed");
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    // 직무, 부서
    public sealed class Assignment
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class UserRow : INotifyPropertyChanged
{
    public long UserOid { get; init; }
    public string UserName { get; init; } = "";
    private bool _pressLevel;

    private string? _assignmentCode;

    public string? AssignmentCode
    {
        get => _assignmentCode;
        set
        {
            if (_assignmentCode == value) return;
            _assignmentCode = value;
            OnPropertyChanged();
        }
    }

    private bool _isAdmin;

    public bool IsAdmin
    {
        get => _isAdmin;
        set
        {
            if (_isAdmin == value) return;
            _isAdmin = value;

            if (!_pressLevel)
            {
                _pressLevel = true;
                if (value) _isGeneral = false;
                OnPropertyChanged(nameof(IsGeneral));
                _pressLevel = false;
            }

            OnPropertyChanged();
        }
    }

    private bool _isGeneral;

    public bool IsGeneral
    {
        get => _isGeneral;
        set
        {
            if (_isGeneral == value) return;
            _isGeneral = value;

            if (!_pressLevel)
            {
                _pressLevel = true;
                if (value) _isAdmin = false;
                OnPropertyChanged(nameof(IsAdmin));
                _pressLevel = false;
            }

            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}