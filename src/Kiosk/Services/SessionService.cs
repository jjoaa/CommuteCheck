using Kiosk.Services.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//세션관리
namespace Kiosk.Services
{
    public class SessionService : ISessionService
    {
        private SessionInfo _session;

        public void SetSession(SessionInfo session) => _session = session;

        public SessionInfo GetSession()
        {
            if (_session == null)
                throw new InvalidOperationException("세션 정보가 없습니다. 로그인 후 사용하세요.");

            return _session;
        }

        public SessionInfo GetValidatedSession()
        {
            var s = GetSession();
            ValidateSession(s);
            return s;
        }

        public void Invalidate() => _session = null;
        public void ClearSession() => _session = null;

        // 공통 검증 로직
        private static void ValidateSession(SessionInfo session)
        {
            if (session == null) throw new InvalidOperationException("유효한 세션이 없습니다.");
            if (string.IsNullOrEmpty(session.SessionId)) throw new InvalidOperationException("세션 ID가 없습니다.");
            if (string.IsNullOrEmpty(session.PublicKey)) throw new InvalidOperationException("서버 PublicKey가 없습니다.");
            if (session.HostOid == null || session.DeviceOid == null)
                throw new InvalidOperationException("호스트/디바이스 OID가 없습니다.");
            if (session.HostLocationOid == null) throw new InvalidOperationException("호스트 위치 정보가 없습니다.");
            if (string.IsNullOrEmpty(session.DeviceId)) throw new InvalidOperationException("DeviceId가 없습니다.");
        }
    }
}