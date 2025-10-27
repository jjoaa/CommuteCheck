using Firebase.Auth;
using Firebase.Auth.Providers;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Database.Streaming;
using Kiosk.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Kiosk.Models;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;

namespace Kiosk.Services
{
    public class FirebaseService
    {
        private FirebaseAuthClient _authClient;
        private FirebaseClient _client;
        private readonly ILogger<FirebaseService> _logger;

        private readonly string FirebaseUrl = Define.FIREBASE_DATABASE_URL;
        private readonly string FirebaseApiKey = Define.FIREBASE_API_KEY;

        // 위치별 구독/컬렉션 풀
        private readonly Dictionary<string, UsersSubscription> _usersSubs = new();

        public FirebaseService(ILogger<FirebaseService> logger)
        {
            _logger = logger;
        }

        public async Task InitializeFirebase()
        {
            _authClient = new FirebaseAuthClient(new FirebaseAuthConfig
            {
                ApiKey = FirebaseApiKey,
                AuthDomain = Define.FIREBASE_AUTH_DOMAIN,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            });

            var auth = await _authClient.SignInWithEmailAndPasswordAsync(Define.AUTH_EMAIL, Define.AUTH_PW);
            if (auth?.User == null) throw new Exception("Firebase 인증 실패");

            _client = new FirebaseClient(FirebaseUrl, new FirebaseOptions
            {
                AuthTokenAsyncFactory = async () => await auth.User.GetIdTokenAsync()
            });

            _logger.LogInformation("[Firebase] 인증 및 클라이언트 초기화 완료");
        }

        // ====== 공유 구독 API  ======
        public async Task<ObservableCollection<WaitUser>> AttachUsersAsync(string hostLocationOid)
        {
            if (_client == null) throw new InvalidOperationException("Firebase not initialized.");

            if (_usersSubs.TryGetValue(hostLocationOid, out var sub))
            {
                sub.RefCount++;
                return sub.Users;
            }

            // 신규 구독 생성
            var usersRef = _client.Child("gpass2/kiosk/users").Child($"h{hostLocationOid}");
            var users = new ObservableCollection<WaitUser>();
            var newSub = new UsersSubscription(users);
            _usersSubs[hostLocationOid] = newSub;

            // 1) 초기 스냅샷 → 컬렉션 채우기
            var snap = await usersRef.OnceAsync<JObject>();
            foreach (var s in snap)
            {
                var wu = TryParseWaitUserFromSnapshot(s.Key, s.Object);
                if (wu != null)
                {
                    Application.Current.Dispatcher.Invoke(() => users.Add(wu));
                }
            }

            // 2) 실시간 구독
            newSub.Listener = usersRef.AsObservable<JObject>()
                .Subscribe(e =>
                {
                    // _logger.LogInformation($"[RTDB] Event={e.EventType}, Key={e.Key}, Obj={e.Object?.ToString(Formatting.None)}");
                    if (e.Object == null) return;

                    if (e.EventType == FirebaseEventType.InsertOrUpdate)
                    {
                        var wu = TryParseWaitUserFromSnapshot(e.Key, e.Object);
                        if (wu == null) return;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var existing = users.FirstOrDefault(x => x.UserOid == wu.UserOid);
                            if (existing != null) users.Remove(existing);
                            users.Add(wu);
                        });
                    }
                    else if (e.EventType == FirebaseEventType.Delete)
                    {
                        var userOid = e.Key?.Replace("u", "");
                        if (string.IsNullOrEmpty(userOid)) return;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var existing = users.FirstOrDefault(x => x.UserOid == userOid);
                            if (existing != null) users.Remove(existing);
                        });
                    }
                });

            newSub.RefCount = 1;
            newSub.HostOid = hostLocationOid;
            return users;
        }

        /*중복로그인 감지용*/
        public async Task SubscribeDevices(string hostLocationOid, string currentDeviceId, Action onDuplicateLogin)
        {
            if (_client == null) throw new InvalidOperationException("Firebase not initialized.");

            await _client.Child("gpass2/kiosk/devices").Child($"h{hostLocationOid}").OnceSingleAsync<JObject>();

            _client.Child("gpass2/kiosk/devices").Child($"h{hostLocationOid}")
                .AsObservable<JObject>()
                .Subscribe(e =>
                {
                    var newDeviceId = e.Object?.Value<string>("device_id");
                    if (!string.IsNullOrEmpty(newDeviceId) && newDeviceId != currentDeviceId)
                    {
                        _logger.LogWarning($"[중복 로그인] 기존: {currentDeviceId}, 새로 등록: {newDeviceId}");
                        onDuplicateLogin?.Invoke();
                    }
                });
        }

        /*대기자 추가*/
        public async Task<WaitUser?> UpdateUserQueue(
            string hostLocationOid, string userOid,
            bool requireName = true, bool requireLandmark = true,
            int timeoutMs = 40000)
        {
            if (_client == null) throw new InvalidOperationException("Firebase not initialized.");

            var secNode = _client.Child("gpass2/kiosk/users")
                .Child($"h{hostLocationOid}")
                .Child($"u{userOid}")
                .Child("secureData");

            bool Satisfy(WaitUser? wu)
            {
                if (wu == null) return false;
                bool okName = !requireName || !string.IsNullOrWhiteSpace(wu.Name);
                bool okLm = !requireLandmark || !string.IsNullOrWhiteSpace(wu.Landmark);
                return okName && okLm;
            }

            WaitUser? Parse(string? secure)
            {
                if (string.IsNullOrWhiteSpace(secure)) return null;
                var cipher = secure.Trim('"'); // 따옴표 방어
                string dec;
                try
                {
                    dec = CryptoUtils.Decrypt(cipher);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[WaitForUserSecureData] decrypt fail: {ex.Message}");
                    return null;
                }

                try
                {
                    var wu = JsonConvert.DeserializeObject<WaitUser>(dec);
                    if (wu != null) wu.UserOid = userOid;
                    return wu;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"[WaitForUserSecureData] json parse fail: {ex.Message}");
                    return null;
                }
            }

            // 1) 초기 스냅샷
            try
            {
                var cur = await secNode.OnceSingleAsync<string>();
                var wu = Parse(cur);
                if (Satisfy(wu)) return wu;
            }
            catch
            {
                /* ignore */
            }

            // 2) 실시간 구독 대기
            var tcs = new TaskCompletionSource<WaitUser?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource(timeoutMs);

            var disp = secNode.AsObservable<string>().Subscribe(e =>
            {
                try
                {
                    var wu = Parse(e.Object);
                    if (Satisfy(wu)) tcs.TrySetResult(wu);
                }
                catch
                {
                    /* ignore */
                }
            });

            using (cts.Token.Register(() => tcs.TrySetResult(null)))
            {
                var result = await tcs.Task;
                disp.Dispose();
                return result; // null이면 타임아웃
            }
        }

        // === 기존 파싱 로직 재사용 헬퍼 ===
        private static string StripU(string key) => (key ?? "").Replace("u", "");

        private WaitUser? TryParseWaitUserFromSnapshot(string key, JObject obj)
        {
            try
            {
                var sd = obj.Value<string>("secureData")
                         ?? obj.SelectToken("secureData")?.Value<string>();
                if (string.IsNullOrEmpty(sd)) return null;

                var wu = ParseWaitUserFromSecure(sd, StripU(key));
                if (wu == null || wu.Level == "-1") return null;
                return wu;
            }
            catch
            {
                return null;
            }
        }

        // 분리
        public void DetachUsers(string hostLocationOid)
        {
            if (_usersSubs.TryGetValue(hostLocationOid, out var sub))
            {
                sub.RefCount--;
                if (sub.RefCount <= 0)
                {
                    sub.Listener?.Dispose();
                    _usersSubs.Remove(hostLocationOid);
                }
            }
        }

        // 공통 헬퍼 : secureData 복호화 → WaitUser
        private WaitUser? ParseWaitUserFromSecure(string? secure, string userOid)
        {
            if (string.IsNullOrWhiteSpace(secure)) return null;

            var cipher = secure.Trim('"');
            string dec;
            try
            {
                dec = CryptoUtils.Decrypt(cipher);
            }
            catch
            {
                return null;
            }

            try
            {
                var wu = JsonConvert.DeserializeObject<WaitUser>(dec);
                if (wu == null) return null;
                wu.UserOid = userOid;
                return wu;
            }
            catch
            {
                return null;
            }
        }

        /*//ImageUrl 업로드
        public async Task PublishWinImageUrlAsync(string hostLocationOid, string imageUrl,
            CancellationToken ct = default)
        {
            if (_client == null) throw new InvalidOperationException("Firebase not initialized.");

            // gpass2/kiosk/users/h{hostLocationOid}/WS3 = "https://..."
            await _client
                .Child("gpass2/kiosk/users")
                .Child($"h{hostLocationOid}")
                .Child("WS3")
                .PutAsync(imageUrl);
        }*/

        // 구독 상태 모델
        private sealed class UsersSubscription
        {
            public UsersSubscription(ObservableCollection<WaitUser> users)
            {
                Users = users;
            }

            public string HostOid { get; set; }
            public ObservableCollection<WaitUser> Users { get; }
            public IDisposable Listener { get; set; }
            public int RefCount { get; set; }
        }
    }
}