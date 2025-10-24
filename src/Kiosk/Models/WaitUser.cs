using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Models
{
    public class WaitUser
    {
        [JsonProperty("name")]
        public string Name { get; set; }= ""; //사용자 이름

        [JsonProperty("level")]
        public string Level { get; set; } = ""; //사용자 레벨

        [JsonProperty("landmark")]  
        public string Landmark { get; set; } = ""; // 얼굴 특징 데이터

        [JsonProperty("phoneNo")]  
        public string PhoneNo { get; set; } = "";  //전화번호

        [JsonProperty("queue_Oid")]
        public string QueueOid { get; set; } = ""; //대기열 ID

        [JsonProperty("commuteType")]
        public string CommuteType { get; set; } = ""; // 출퇴근 유형

        [JsonIgnore]
        public string UserOid { get; set; } = "";//사용자 고유 ID

        // UI 표시용 익명 처리된 이름
        public string MaskedName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                    return string.Empty;

                var s = Name.Trim();

                // ✅ 숫자만으로 구성된 경우(예: "1533")는 그대로 반환
                if (s.All(char.IsDigit))
                    return s;

                // 아래는 한글/이름 마스킹 규칙
                if (s.Length < 2) return s;
                if (s.Length == 2) return $"{s[0]}*";

                // 3글자 이상이면 가운데(들) * 처리
                var chars = s.ToCharArray();
                for (int i = 1; i < chars.Length - 1; i++)
                    chars[i] = '*';
                return new string(chars);
            }
        }
    }
}
