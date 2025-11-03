using System;
using System.Globalization;


namespace Kiosk.Utils
{
    //문자열을 float[512]로 바꾸는 파싱
    public static class EmbeddingParser
    {
        /// <summary>
        /// "0.12, -0.03, ..." 또는 "[0.12, -0.03, ...]" → float[expectedDim]
        /// 실패 시 null.
        /// </summary>
        public static float[] ParseEmbedding(string s, int expectedDim = 512)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            s = s.Trim();
            if (s.StartsWith("[") && s.EndsWith("]"))
                s = s.Substring(1, s.Length - 2);

            var parts = s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != expectedDim) return null;

            var vec = new float[expectedDim];
            for (int i = 0; i < expectedDim; i++)
            {
                var token = parts[i].Trim();
                if (!float.TryParse(token,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out vec[i]))
                {
                    return null;
                }
                if (float.IsNaN(vec[i]) || float.IsInfinity(vec[i])) return null;
            }
            return vec;
        }
    }
}