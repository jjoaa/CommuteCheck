using Firebase.Database.Streaming;
using Kiosk.Commands;
using Kiosk.FaceEngine;
using Kiosk.Models;
using Kiosk.Services;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Navigation;

//위치 리스트를 가져와 화면에 바인딩
//사용자가 위치 선택 완료하면 OnComplete() 호출 

namespace Kiosk.ViewModels
{
    public class LocationSelectionViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly ILocationService _locationService;
        private readonly IBeaconPublisher _beaconPublisher;
        private readonly ISessionService _sessionService;
        private readonly FirebaseService _firebase;

        private readonly string _uuid = Define.UUID;
        public ObservableCollection<LocationModel> Locations { get; set; }
        public ObservableCollection<WaitUser> WaitUsers { get; set; } = new();
        public IAsyncCommand SelectCommand { get; }
        public IAsyncCommand CompleteCommand { get; }

        private LocationModel _selectedLocation;

        public LocationModel SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (_selectedLocation == value) return;

                if (_selectedLocation != null) _selectedLocation.IsSelected = false;
                _selectedLocation = value;
                if (_selectedLocation != null) _selectedLocation.IsSelected = true;

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocationSelected));
                OnPropertyChanged(nameof(SelectedLocationText));

                (CompleteCommand as IAsyncCommand)?.NotifyCanExecuteChanged();
            }
        }

        public bool IsLocationSelected => SelectedLocation != null;
        public string SelectedLocationText => SelectedLocation?.LocationName ?? "";
        private readonly bool _isReturnFromFaceRecognition;
        private readonly LocationModel _returnedLocation;

        public LocationSelectionViewModel(
            ILocationService locationService,
            IBeaconPublisher beaconPublisher,
            VmDeps<LocationSelectionViewModel> deps,
            FirebaseService firebase)
        {
            _locationService = locationService;
            _beaconPublisher = beaconPublisher;
            _firebase = firebase;
            _sessionService = deps.Session;
            _navigationService = deps.Nav;

            _ = InitializeLocationsAsync();

            SelectCommand = new AsyncRelayCommand<LocationModel>(async item =>
            {
                if (item == null) return;
                SelectedLocation = item;
                await OnComplete();
            });
            CompleteCommand = new AsyncRelayCommand(
                OnComplete,
                () => IsLocationSelected
            );
        }

        private async Task InitializeLocationsAsync()
        {
            var session = _sessionService.GetSession();
            var locations = await _locationService.GetLocationsByHostOidAsync();
            Locations = new ObservableCollection<LocationModel>(locations);
            OnPropertyChanged(nameof(Locations));
        }

        private async Task OnComplete()
        {
            var myDeviceId = PreferenceUtils.GetString("DeviceId");
            if (SelectedLocation == null) return;

            try
            {
                var old = _sessionService.GetSession()?.HostLocationOid;
                if (old.HasValue)
                    _firebase.DetachUsers(old.Value.ToString());

                // 1. Firebase 초기화
                await _firebase.InitializeFirebase();

                //2. 세션 업데이트
                var s = _sessionService.GetSession() ?? new SessionInfo();
                s.HostLocationOid = SelectedLocation.HostLocationOid;
                s.LocationName = SelectedLocation.LocationName;
                _sessionService.SetSession(s);

                //3. 중복 로그인 감시
                await _firebase.SubscribeDevices(SelectedLocation.HostLocationOid.ToString(), myDeviceId,
                    HandleDuplicateLogin);

                // 4. Beacon 광고 시작
                _beaconPublisher.StartIBeacon(_uuid,
                    (ushort)SelectedLocation.MajorId, // major_id (SOAP 응답에서 가져온 값)
                    (ushort)SelectedLocation.MinorId // minor_id (SOAP 응답에서 가져온 값)
                );
                App.Logger.LogInformation($"[Beacon] 광고 시작: major_id={SelectedLocation.MajorId}");

                // 5. 얼굴인식 페이지로 이동
                _navigationService.NavigateTo<FaceRecognitionViewModel>(
                    new FaceRecognitionModel
                    {
                        HostLocationOid = SelectedLocation.HostLocationOid,
                        LocationName = SelectedLocation.LocationName,
                    });
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Firebase/Beacon 연동 실패: {ex.Message}");
            }
        }

        //사용자 데이터 구독
        private void OnUserDataChanged(FirebaseEvent<JObject> e)
        {
            if (e.EventType == FirebaseEventType.InsertOrUpdate && e.Object != null)
            {
                try
                {
                    var secureData = e.Object.SelectToken("secureData")?.ToString();
                    WaitUser user;

                    if (!string.IsNullOrEmpty(secureData))
                    {
                        var decryptedJson = CryptoUtils.Decrypt(secureData);
                        user = JsonConvert.DeserializeObject<WaitUser>(decryptedJson);
                        App.Logger.LogInformation($"[Users] Decrypted User: {user?.Name}");

                        if (user != null && user.Level != "-1")
                        {
                            user.UserOid = e.Key.Replace("u", "");
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                var existing = WaitUsers.FirstOrDefault(u => u.UserOid == user.UserOid);
                                if (existing != null)
                                    WaitUsers.Remove(existing);
                                WaitUsers.Add(user);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.LogError($"[Users] 파싱 실패: {ex.Message}");
                }
            }
            else if (e.EventType == FirebaseEventType.Delete)
            {
                string targetUserOid = e.Key.Replace("u", "");
                App.Current.Dispatcher.Invoke(() =>
                {
                    var userToRemove = WaitUsers.FirstOrDefault(u => u.UserOid == targetUserOid);
                    if (userToRemove != null)
                        WaitUsers.Remove(userToRemove);
                });
            }
        }

        private void HandleDuplicateLogin()
        {
            App.Logger.LogWarning("[중복 로그인] 감지됨 → 세션 초기화 및 재로그인 유도");
            App.Current.Dispatcher.Invoke(() => { PreferenceUtils.Clear(); });
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}