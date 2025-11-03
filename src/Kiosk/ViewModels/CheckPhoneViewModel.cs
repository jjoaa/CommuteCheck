using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Services.Interface;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Kiosk.Commands;

namespace Kiosk.ViewModels
{
    public class CheckPhoneViewModel : INotifyPropertyChanged
    {
        private readonly ICheckPhoneService _checkPhoneService;
        private readonly INavigationService _navigationService;
        private readonly ISessionService _sessionService;
        private readonly FirebaseService _firebase;
        private readonly ILogger<CheckPhoneViewModel> _logger;

        private string _phoneNumber = "010";

        private CheckPhoneModel _currentUserModel;

        // 사용자 정보 바인딩용 속성들 추가
        public string UserName { get; private set; }
        public string UserPhoneNumber { get; private set; }
        public string UserLandmark { get; private set; }
        public ICommand CloseCommand { get; }

        public string PhoneNumber
        {
            get => _phoneNumber;
            set
            {
                // 숫자만 유지
                var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
                if (digits.Length > 11) digits = digits.Substring(0, 11);

                if (_phoneNumber == digits) return;
                _phoneNumber = digits;
                OnPropertyChanged(); // PhoneNumber
                OnPropertyChanged(nameof(PhoneNumberFormatted)); // 포맷 표시용도 갱신

                // 11자리 완성 시 비동기로 처리(아래 B 참고)
                if (digits.Length == 11 && !_submitting)
                    _ = SubmitAndResetAsync(digits);
            }
        }

        // 화면 표시용
        public string PhoneNumberFormatted
        {
            get
            {
                var d = _phoneNumber ?? "";
                if (d.Length == 0) return "010";
                if (d.Length <= 3) return d;
                if (d.Length <= 7) return $"{d[..3]}-{d[3..]}";
                return $"{d[..3]}-{d.Substring(3, 4)}-{d[7..]}";
            }
        }

        private bool _submitting;

        private async Task SubmitAndResetAsync(string digitsOnly)
        {
            try
            {
                _submitting = true;

                var result = await _checkPhoneService.UpdateUserQueueAsync(digitsOnly);

                if (result.Success)
                {
                    // 정상 케이스
                    var model = await MakeFaceModelWithUsersAsync(
                        initialStatus: "화면에 얼굴을 위치시켜주세요",
                        revertSec: 0
                    );
                    _navigationService.NavigateTo<FaceRecognitionViewModel>(model);
                }
                else
                {
                    string status;
                    int revertSec = 0;

                    switch (result.Type)
                    {
                        case "ERROR_USER":
                            // INVALID_USER
                            status = "가입되지 않았습니다. 관리자에게 문의하세요";
                            revertSec = 5;
                            break;

                        case "ERROR_TIMEOUT":
                            // 10초 초과(핸드폰 미응답/서버 미응답)
                            status = "확인이 불가능합니다. 관리자에게 문의하세요";
                            revertSec = 5;
                            break;

                        case "ERROR_NOT_REGISTERED_ON_DEVICE":
                            // 서버/디바이스에 사용자 정보가 없어 실시간 응답을 못 받은 경우
                            status = "확인이 불가능합니다. 관리자에게 문의하세요";
                            revertSec = 5;
                            break;

                        default:
                            status = "오류가 발생했습니다. 관리자에게 문의하세요";
                            revertSec = 5;
                            break;
                    }

                    var model = await MakeFaceModelWithUsersAsync(
                        initialStatus: status,
                        revertSec: revertSec
                    );
                    _navigationService.NavigateTo<FaceRecognitionViewModel>(model);
                }
            }
            finally
            {
                // 마지막에 한 번만 리셋 → 바인딩 통해 화면 반영
                PhoneNumber = "010";
                _submitting = false;
            }
        }

        private void ResetPhoneNumber() => PhoneNumber = "010";

        public CheckPhoneViewModel(
            ICheckPhoneService checkPhoneService,
            FirebaseService firebase,
            VmDeps<CheckPhoneViewModel> deps)
        {
            _checkPhoneService = checkPhoneService;
            _firebase = firebase;
            _logger = deps.Logger;
            _sessionService = deps.Session;
            _navigationService = deps.Nav;

            // 닫기 버튼도 동일 로직으로 얼굴인식 페이지로 진입 가능
            CloseCommand = new AsyncRelayCommand(async () =>
            {
                var model = await MakeFaceModelWithUsersAsync("가까이/정면/조명 확인해주세요", 0);
                _navigationService.NavigateTo<FaceRecognitionViewModel>(model);
            });
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

        private void HandleRegistrationFailure()
        {
            ResetPhoneNumber();
        }

        // INotifyPropertyChanged 구현
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}