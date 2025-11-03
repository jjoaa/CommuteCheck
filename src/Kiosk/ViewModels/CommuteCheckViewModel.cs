using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using System.Windows.Input;
using Kiosk.Commands;
using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Services.Interface;

namespace Kiosk.ViewModels;

public class CommuteCheckViewModel : INotifyPropertyChanged, INavigationAware
{
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly ILogger<CommuteCheckViewModel> _logger;
    private readonly ICommuteService _commute;
    private readonly FirebaseService _firebase;

    private CommuteCheckModel.InitCommuteCheckModel? InitModel { get; set; }
    public bool IsAdmin { get; private set; }
    private bool _busy;

    public DateTime AuthTime { get; set; }
    public string AuthTimeText => $"인증시간 : {AuthTime:yyyy.MM.dd(ddd) tt hh:mm}";
    public string UserName => InitModel?.UserName ?? string.Empty;

    //미싱펀치 방지 + 중복 퇴근 차단
    private int _submitGate = 0; // 0=열림, 1=닫힘
    private volatile bool _punchBusy = false;
    private DateTime _lastPunchAt = DateTime.MinValue;
    private static readonly TimeSpan PunchCooldown = TimeSpan.FromSeconds(5);

    private readonly IPunchStateCache _punchCache;
    private bool _canCheckIn = false;
    private bool _canCheckOut = false;
    private string CurLoc() => InitModel!.HostLocationOid;

    public AsyncRelayCommand CheckInCommand { get; }
    public AsyncRelayCommand CheckOutCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    private void SetBusy(bool v)
    {
        _busy = v;
        (CheckInCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
        (CheckOutCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
        (BackCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public CommuteCheckViewModel(VmDeps<CommuteCheckViewModel> deps,
        ICommuteService commute,
        FirebaseService firebase,
        IPunchStateCache punchCache)
    {
        _logger = deps.Logger;
        _sessionService = deps.Session;
        _navigationService = deps.Nav;
        _commute = commute;
        _firebase = firebase;
        _punchCache = punchCache;

        CheckInCommand = new AsyncRelayCommand(
            async () => { await SubmitAsync(CommuteType.CheckIn); },
            () => !_busy && !_punchBusy && _canCheckIn
        );
        CheckOutCommand = new AsyncRelayCommand(
            async () => { await SubmitAsync(CommuteType.CheckOut); },
            () => !_busy && !_punchBusy && _canCheckOut
        );
        BackCommand = new AsyncRelayCommand(async () =>
        {
            var model = await MakeFaceModelWithUsersAsync(
                initialStatus: "화면에 얼굴을 위치시켜주세요",
                revertSec: 0);

            _navigationService.NavigateTo<FaceRecognitionViewModel>(model);
        }, () => !_busy);

        OpenSettingsCommand = new RelayCommand(OpenSettings, () => !_busy);
    }

    private async Task<FaceRecognitionModel> MakeFaceModelWithUsersAsync(string initialStatus, int revertSec)
    {
        var s = _sessionService.GetSession();
        var hostOid = s?.HostLocationOid ?? 0;
        var users = await _firebase.AttachUsersAsync(hostOid.ToString());

        return new FaceRecognitionModel
        {
            HostLocationOid = hostOid,
            LocationName = s?.LocationName ?? (hostOid > 0 ? $"#{hostOid}" : ""),
            StatusText = initialStatus,
            StatusAutoRevertSec = revertSec,
            WaitUsers = users
        };
    }

    public void OnNavigatedTo(object? parameter)
    {
        if (parameter is CommuteCheckModel.InitCommuteCheckModel m)
        {
            InitModel = m;
            AuthTime = m.AuthTime;
            //  5 또는 9면 관리자
            IsAdmin = (m.UserLevel == "5");
            _canCheckIn = true;
            _canCheckOut = false;
            UpdateButtonsFromCache(DateTime.UtcNow);

            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(AuthTimeText));
            OnPropertyChanged(nameof(IsAdmin));
        }
    }

    private async Task SubmitAsync(CommuteType type)
    {
        if (InitModel == null) return;
        var now = DateTime.UtcNow;
        if (_busy || _punchBusy) return;
        if (now - _lastPunchAt < PunchCooldown) return;
        if (Interlocked.Exchange(ref _submitGate, 1) == 1) return;

        _punchBusy = true;
        _lastPunchAt = now;

        try
        {
            SetBusy(true);
            var state = _punchCache.Get(InitModel.UserOid, CurLoc(), now);

            if (type == CommuteType.CheckOut)
            {
                // 0) 이미 오늘 퇴근 완료(로컬 캐시 기준)
                if (state.Closed)
                {
                    var msg = state.LastCheckoutUtc.HasValue
                        ? BuildAlreadyCheckedOutMessage(state.LastCheckoutUtc.Value)
                        : "오늘 퇴근 기록이 있습니다. 추가 근무를 기록하려면 출근을 눌러주세요.";
                    MessageBox.Show(msg, "퇴근", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateButtonsFromCache(now);
                    return;
                }

                async Task<bool> DoBackToBackAsync()
                {
                    using var ctsIn = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var inResp = await PunchAsync("0", "0", ctsIn.Token); // CheckIn

                    // 성공 or 이미 출근상태(101) 둘 다 OK로 간주
                    if (inResp.Success || inResp.ResultCode == "100" || IsInvalidType(inResp))
                    {
                        _punchCache.MarkOpen(InitModel!.UserOid, CurLoc(), now); // ★

                        using var ctsOut = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                        var outResp = await PunchAsync("1", "0", ctsOut.Token); // CheckOut
                        if (outResp.Success || outResp.ResultCode == "100")
                        {
                            _punchCache.MarkClosed(InitModel!.UserOid, CurLoc(), now, DateTime.UtcNow); // ★

                            return true;
                        }
                    }

                    return false;
                }

                // 1) 로컬 캐시상 OPEN이 아니면: 안내 후 백투백 옵션
                if (!state.HasOpen)
                {
                    var r = MessageBox.Show(
                        "출근 기록이 없어 퇴근을 처리할 수 없습니다.\n지금 출근 후 바로 퇴근 처리할까요?",
                        "퇴근",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information,
                        MessageBoxResult.Yes);

                    if (r == MessageBoxResult.Yes)
                    {
                        var ok = await DoBackToBackAsync();
                        if (ok) goto NAVIGATE;

                        MessageBox.Show("처리에 실패했습니다. 잠시 후 다시 시도해주세요.",
                            "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }

                    UpdateButtonsFromCache(now);
                    return;
                }

                // 2) 평상 케이스: OPEN이면 바로 퇴근 시도
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                {
                    var outResp = await PunchAsync("1", "0", cts.Token); // CheckOut

                    if (outResp.Success || outResp.ResultCode == "100")
                    {
                        _punchCache.MarkClosed(InitModel!.UserOid, CurLoc(), now, DateTime.UtcNow); // ★

                        goto NAVIGATE;
                    }

                    // 서버 기준 OPEN이 아닌 경우(캐시가 뒤쳐졌을 수 있음) → 안내 후 백투백 제안
                    if (IsInvalidType(outResp)) // == 101
                    {
                        var r = MessageBox.Show(
                            "출근 기록이 확인되지 않습니다.\n지금 출근 후 바로 퇴근 처리할까요?",
                            "퇴근",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information,
                            MessageBoxResult.Yes);

                        if (r == MessageBoxResult.Yes && await DoBackToBackAsync())
                            goto NAVIGATE;

                        UpdateButtonsFromCache(now);
                        return;
                    }

                    MessageBox.Show("퇴근 처리에 실패했습니다. 잠시 후 다시 시도해주세요.",
                        "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateButtonsFromCache(now);
                    return;
                }
            }
            else // type == CheckIn
            {
                // 출근
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var resp = await PunchAsync("0", "0", cts.Token);

                if (resp.Success || resp.ResultCode == "100")
                {
                    _punchCache.MarkOpen(InitModel.UserOid, CurLoc(), now);
                }
                else if (IsInvalidType(resp)) // == 101 (이미 출근 상태)
                {
                    // 이미 OPEN 상태로 간주 → 버튼 전환 + 안내
                    _punchCache.MarkOpen(InitModel.UserOid, CurLoc(), now);
                    UpdateButtonsFromCache(now);

                    MessageBox.Show(
                        "이미 출근 상태입니다.\n퇴근을 눌러서 근무를 종료해주세요.",
                        "출근",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    // 다음 클릭(퇴근)을 바로 허용
                    _lastPunchAt = DateTime.MinValue; // 쿨다운 해제
                    System.Threading.Volatile.Write(ref _submitGate, 0);
                    _punchBusy = false;
                    (CheckInCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
                    (CheckOutCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
                    return;
                }
                else if (string.Equals(resp.Result, "-INVALID_USER", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("이 지점에서 등록되지 않은 사용자입니다.\n관리자에게 문의하세요.",
                        "출근", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateButtonsFromCache(now);
                    return;
                }
                else
                {
                    // 기타 에러 처리
                    MessageBox.Show("출근 처리에 실패했습니다. 잠시 후 다시 시도해주세요.",
                        "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            NAVIGATE:
            // 성공/처리 후 UI 잠금 전이
            UpdateButtonsFromCache(now);
            var faceModel = await MakeFaceModelWithUsersAsync("화면에 얼굴을 일치시켜주세요", 0);
            _navigationService.NavigateTo<FaceRecognitionViewModel>(faceModel);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "출/퇴근 처리 실패");
        }
        finally
        {
            SetBusy(false);
            System.Threading.Volatile.Write(ref _submitGate, 0);
            _punchBusy = false; // 쿨다운은 _lastPunchAt로 제어
        }
    }

    private void UpdateButtonsFromCache(DateTime nowUtc)
    {
        var s = _punchCache.Get(InitModel!.UserOid, CurLoc(), nowUtc);

        if (s.Closed)
        {
            _canCheckIn = true; // 퇴근 완료면 추가 근무는 출근부터
            _canCheckOut = false;
        }
        else
        {
            _canCheckIn = !s.HasOpen; // open 없으면 출근 가능
            _canCheckOut = s.HasOpen; // open 있으면 퇴근 가능
        }

        (CheckInCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
        (CheckOutCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    //페어 펀치
    private async Task<CommuteCheckModel.CommuteResponse> PunchAsync(
        string type, string isReUpdate, CancellationToken ct)
    {
        var req = new CommuteCheckModel.CommuteRequest
        {
            HostLocationOid = InitModel!.HostLocationOid,
            UserOid = InitModel!.UserOid,
            CommuteType = type, // "0"=출근, "1"=퇴근
            IsReUpdate = isReUpdate
        };

        var resp = await _commute.SubmitAsync(req, ct);
        _logger.LogInformation("[Commute] {Type}/{ReUpd} => {Result}/{Code} (ok={Ok})",
            type, isReUpdate, resp.Result, resp.ResultCode, resp.Success);

        return resp;
    }

    private static bool IsInvalidType(CommuteCheckModel.CommuteResponse r)
        => string.Equals(r.Result, "-INVALID_COMMUTE_TYPE", StringComparison.OrdinalIgnoreCase)
           && string.Equals(r.ResultCode, "101", StringComparison.OrdinalIgnoreCase);

    private void OpenSettings()
    {
        if (InitModel == null) return;

        var init = new SettingsModel.InitSettingsModel
        {
            UserName = InitModel.UserName ?? string.Empty,
            HostLocationOid = long.Parse(InitModel.HostLocationOid),
            PageNo = 1
        };
        _navigationService.NavigateTo<SettingsViewModel>(init);
    }

    private static string BuildAlreadyCheckedOutMessage(DateTime lastCheckoutUtc)
    {
        var kst = TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        var t = TimeZoneInfo.ConvertTimeFromUtc(lastCheckoutUtc, kst);
        return $"오늘 퇴근({t:HH:mm}) 기록이 있습니다. 추가 근무를 기록하려면 출근을 눌러주세요.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}