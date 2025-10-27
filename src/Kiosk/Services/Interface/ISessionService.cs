using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//세션 관리
namespace Kiosk.Services.Interface
{
    public interface ISessionService
    {
        void SetSession(SessionInfo session);
        SessionInfo GetSession();
        SessionInfo GetValidatedSession(); // 검증된 세션 반환
        void Invalidate(); // 세션 무효화
        void ClearSession();
    }

    public class SessionInfo
    {
        public string PublicKey { get; set; }
        public string SessionId { get; set; }
        public string HostOid { get; set; }
        public string DeviceOid { get; set; }
        public string DeviceId { get; set; }
        public string AppMode { get; set; }
        public long? HostLocationOid { get; set; }
        public string? LocationName { get; set; }
    }
}