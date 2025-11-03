using Kiosk.Models;
using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;
using Kiosk.Commands;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kiosk.Services;
using static Kiosk.Utils.BitmapUtils;
using static Kiosk.Utils.ROIUtils;

namespace Kiosk.ViewModels
{
    public class FaceRecognitionViewModel : INotifyPropertyChanged
    {
        private readonly ISessionService _sessionService;
        private readonly INavigationService _navigationService;
        private readonly ILogger<FaceRecognitionViewModel> _logger;
        private readonly FirebaseService _firebaseService;
        
        private readonly ICameraService _cameraService;
        private readonly IFacialResultService _facialResultService;
        //private readonly IFaceRecognitionService _faceService;
        private readonly IImageUrlService _imageUrlService;

        public FaceRecognitionModel Model { get; set; } = new();
        
        private int _frameCount = 0;
        private bool _navigated, _busy, _didPreWarm = false;
        
        private DispatcherTimer _clock; // DTO로 받는 파라미터
        private bool _usersAttached = false;
        
        // 품질 선별용
        private volatile Bitmap? _bestShot;
        private double _bestScore = 0;
        private readonly object _bestLock = new();

        // 베스트샷 유효 기간(너무 오래된 컷 방지)
        private readonly TimeSpan _bestShotTtl = TimeSpan.FromSeconds(8);
        private DateTime _bestShotAt = DateTime.MinValue;

        private bool _didUploadThisAuth = false;   // 중복 업로드 방지

        // 카메라 이미지
        private BitmapImage _cameraFrame;

        public BitmapImage CameraFrame
        {
            get => _cameraFrame;
            set
            {
                _cameraFrame = value;
                OnPropertyChanged();
            }
        }

        private string _statusText = "화면에 얼굴을 위치시켜주세요";

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public FaceRecognitionViewModel(
            VmDeps<FaceRecognitionViewModel> deps,
            FirebaseService firebase,
            
            ICameraService cameraService,
            IFaceRecognitionService faceService,
            IFacialResultService facialResultService,
            IImageUrlService imageUrlService
            )
        {
            _logger = deps.Logger;
            _sessionService = deps.Session;
            _navigationService = deps.Nav;
            _firebaseService = firebase;
            
            _cameraService = cameraService;
            _facialResultService = facialResultService;
            //_faceService = faceService;
            _imageUrlService = imageUrlService;

            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (_, __) =>
            {
                if (Model != null)
                {
                    Model.CurrentDateTime = DateTime.Now;

                    if (Model.StatusAutoRevertSec > 0)
                    {
                        Model.StatusAutoRevertSec--;
                        if (Model.StatusAutoRevertSec == 0)
                        {
                            Model.StatusText = "화면에 얼굴을 위치시켜주세요";
                        }
                    }
                }
            };
            _clock.Start();
        }

        /*private async void OnFrameCaptured(object? sender, Bitmap bitmap)
        {
            try
            {
                using var uiBmp = DeepCopy(bitmap);
                using var procBmp = DeepCopy(bitmap);

                var frameImage = ToFrozenBitmapImage(uiBmp);
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        CameraFrame = frameImage;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "UI set CameraFrame 실패");
                    }
                }));

                if (!_didPreWarm)
                {
                    _didPreWarm = true;
                    await _faceService.PreWarmAsync(procBmp);
                }

                _frameCount++;
                if (_frameCount % 5 != 0 || _busy || _navigated) return;

                _busy = true;

                var roi = ComputeRoiRectInImage(procBmp); // 처리용만 사용
                using var cropped = CropBitmap(procBmp, roi);

                var result = await _faceService.RecognizeAsync(cropped, Model.WaitUsers);
                var ok = result?.IsSuccess == true;
                
                Application.Current.Dispatcher.BeginInvoke(() =>
                    StatusText = ok ? "인식되었습니다" : "화면에 얼굴을 위치시켜주세요");
                if (!ok) return;

                // 0) 인증 시각(타이머 2초 stale 방어)
                var now = DateTime.Now;
                var timerTime = Model?.CurrentDateTime;
                var authTime = (timerTime.HasValue && Math.Abs((now - timerTime.Value).TotalSeconds) <= 2)
                    ? timerTime.Value
                    : now;
                var matchedUserOid = result.UserOid!;
                var sessionLoc = _sessionService.GetValidatedSession().HostLocationOid ?? 0;

                // (안전)
                if (!_usersAttached || Model.WaitUsers == null)
                    await EnsureUsersAttachedAsync();

                // 1) 지점 일치 보장(세션 지점과 모델 지점 불일치 시 재구독/차단 선택)
                if (Model.HostLocationOid != sessionLoc)
                {
                    _logger.LogWarning("[AuthGate] Location mismatch: model={ModelLoc}, session={SessLoc}",
                        Model.HostLocationOid, sessionLoc);
                    Model.HostLocationOid = sessionLoc;
                    _usersAttached = false;
                    await EnsureUsersAttachedAsync();
                }

                // 2) RTDB 게이트: 이 지점 허용 사용자 여부
                var allowed = Model.WaitUsers?.Any(u => u.UserOid == matchedUserOid) == true;
                if (!allowed)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Model.StatusText = "등록되지 않은 사용자입니다.";
                        Model.StatusAutoRevertSec = 3;
                    }));
                    _logger.LogWarning("[AuthGate] Not allowed at location {Loc}: user={User}",
                        Model.HostLocationOid, matchedUserOid);
                    _navigated = false; // 버튼 페이지로 이동 금지
                    _busy = false; // 다음 프레임 처리 가능
                    return;
                }
                
                // 3) 서버 보고
                var hostLocationOid = sessionLoc.ToString();
                var send = await ReportFacialAsync(result!, hostLocationOid);
                if (send is null)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                        StatusText = "인식 처리 실패. 다시 시도해주세요");
                    return;
                }
                
                if (_navigated) return;
                _navigated = true;

                var finalUserLevel = NormalizeLevel(send.UserLevel ?? "0");

                var model = new CommuteCheckModel.InitCommuteCheckModel()
                {
                    UserOid = send.UserOid ?? result.UserOid!,
                    HostLocationOid = hostLocationOid,
                    UserName = send.UserName ?? result.UserName!,
                    TransactionOid = send.TransactionOid!,
                    AuthTime = authTime,
                    UserLevel = finalUserLevel
                };
                await StopRecognitionAsync();

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _navigationService.NavigateTo<CommuteCheckViewModel>(model);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "네비게이션 실패");
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnFrameCaptured 실패");
            }
            finally
            {
                _busy = false;
            }
        }*/
        
        /*이미지 URL*/
        private async void OnFrameCaptured(object? sender, Bitmap bitmap)
        {
            try
            {
                using var uiBmp = DeepCopy(bitmap);
                using var procBmp = DeepCopy(bitmap);

                // UI 미리보기
                var frameImage = ToFrozenBitmapImage(uiBmp);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => CameraFrame = frameImage));

                _frameCount++;
                if (_frameCount % 5 != 0 || _busy || _navigated) return;
                _busy = true;

                // 1) ROI → 크롭 → 베스트샷 선택
                var roi = ComputeRoiRectInImage(procBmp);
                using var cropped = CropBitmap(procBmp, roi);
                using var best = TakeBestShotAndReset() ?? DeepCopy(cropped);

                // 2) 세션에서 지점 확인
                var sessionLoc = _sessionService.GetValidatedSession().HostLocationOid ?? 0;
                if (Model.HostLocationOid != sessionLoc)
                {
                    _logger.LogWarning("[AuthGate] Location mismatch: model={ModelLoc}, session={SessLoc}", Model.HostLocationOid, sessionLoc);
                    Model.HostLocationOid = sessionLoc;
                }
                // 3) 업로드 + UPDATE
                var up = await _imageUrlService.UploadBestShotAndUpdateAsync(best, CancellationToken.None);
                if (!up.Success || string.IsNullOrWhiteSpace(up.Key))
                {
                    Application.Current.Dispatcher.BeginInvoke(() => {
                        StatusText = "이미지 업로드 실패. 다시 시도해주세요";
                        Model.StatusAutoRevertSec = 3;
                    });
                    return;
                }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                       // _navigationService.NavigateTo<CommuteCheckViewModel>(model);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "네비게이션 실패");
                    }
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OnFrameCaptured 실패");
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task<FacialResultModel.SendFacialResultResponse?> ReportFacialAsync(
            RecognitionResult result, string hostLocationOid, CancellationToken ct = default)
        {
            try
            {
                // 1) 초기 매칭 결과 업데이트 → TxOid 발급
                var upd = await _facialResultService.UpdateFacialDataAsync(new()
                {
                    HostLocationOid = hostLocationOid, // string 유지 OK (빌더에서 number 직렬화)
                    UserOid = result.UserOid!,
                    Dist = result.Dist ?? 0.0,
                    App = "0"
                }, ct);

                if (!upd.Success || string.IsNullOrEmpty(upd.TransactionOid))
                {
                    _logger.LogWarning("UpdateFacialData 실패/txOid 없음: {err}", upd.Error);
                    return null;
                }

                // 2) 최종 보고
                var send = await _facialResultService.SendFacialResultAsync(new()
                {
                    TransactionOid = upd.TransactionOid!, // string 유지 OK (빌더에서 number 직렬화)
                    HostLocationOid = hostLocationOid,
                    UserOid = result.UserOid!,
                    App = "0",
                    Mode = "0", // 0=자동, 1=수동
                    Result = "SUCCESS"
                }, ct);

                if (!send.Success)
                {
                    _logger.LogWarning("SendFacialResult 실패: {err}", send.Error);
                    return null;
                }

                return send;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReportFacialAsync 실패");
                return null;
            }
        }

        public async Task StartRecognitionAsync()
        {
            // 세션의 HostLocationOid로 모델을 강제 동기화
            var s = _sessionService.GetValidatedSession();
            var sessionLoc = s.HostLocationOid ?? 0;
            if (Model.HostLocationOid != sessionLoc)
            {
                Model.HostLocationOid = sessionLoc;
                _usersAttached = false; // location 변경 시 재구독
            }

            await EnsureUsersAttachedAsync();
            _didUploadThisAuth = false; // 중복 업로드 방지
            // 중복 구독 방지(안전장치)
            _cameraService.FrameCaptured -= OnFrameCaptured;
            _cameraService.FrameCaptured += OnFrameCaptured;
           
            // 상태 초기화
            _navigated = false;
            _busy = false;
            _didPreWarm = false;
            _frameCount = 0;

            await _cameraService.OpenCameraAsync();
            _logger.LogInformation("얼굴 인식 시작");
        }

        public async Task StopRecognitionAsync()
        {
            // 이벤트 해제
            _cameraService.FrameCaptured -= OnFrameCaptured;
            try
            {
                await _cameraService.CloseCameraAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CloseCameraAsync 실패");
            }

            // 상태 리셋
            _busy = false;
            _didPreWarm = false;
            _frameCount = 0;
            _logger.LogInformation("얼굴 인식 중지 완료");
        }

        /*Helper*/
        //베스트샷 반환 헬퍼
        private Bitmap? TakeBestShotAndReset()
        {
            lock (_bestLock)
            {
                var shot = _bestShot != null ? DeepCopy(_bestShot) : null; // 호출자가 dispose
                _bestShot?.Dispose();
                _bestShot = null;
                _bestScore = 0;
                _bestShotAt = DateTime.MinValue;
                return shot;
            }
        }
        // 대기자 Attach 보장
        private async Task EnsureUsersAttachedAsync()
        {
            if (_usersAttached) return;
            var loc = Model.HostLocationOid > 0
                ? Model.HostLocationOid
                : _sessionService.GetValidatedSession().HostLocationOid ?? 0;
            if (loc <= 0) return;

            var users = await _firebaseService.AttachUsersAsync(loc.ToString());
            if (!ReferenceEquals(Model.WaitUsers, users))
                Model.WaitUsers = users;

            _usersAttached = true;
        }

        private static string NormalizeLevel(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "0";
            return int.TryParse(raw.Trim(), out var n) ? n.ToString() : raw.Trim();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}