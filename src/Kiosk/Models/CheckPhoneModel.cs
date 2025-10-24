using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Kiosk.Models
{
    public class CheckPhoneModel
    {
        public bool Success { get; set; }
        public string Result { get; set; }
        public string FirebaseToken { get; set; }
        public string Type { get; set; } // 0이면 유저한테 FCM보낸다. 1이면 서버에 저장한다.

        // 대기자 명단 관련 추가 정보
        public string UserName { get; set; }
        public long UserOid { get; set; }
        public long HostLocationOid { get; set; }
        public string Landmark { get; set; }
        public string PhoneNumber { get; set; }
    }
}