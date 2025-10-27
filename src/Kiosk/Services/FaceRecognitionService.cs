using Kiosk.FaceEngine;
using Kiosk.Models;
using Kiosk.Services.Interface;
using Kiosk.Utils;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Microsoft.Extensions.Logging;

namespace Kiosk.Services
{
    public class FaceRecognitionService : IFaceRecognitionService
    {
        // 중복 실행(재진입) 차단용
        private readonly SemaphoreSlim _sem = new(1, 1);

        private bool _preWarmed = false;

        public async Task PreWarmAsync(Bitmap anyFrame)
        {
            if (_preWarmed || anyFrame == null) return;
            await Task.Run(() =>
            {
                try
                {
                    var (bgr, w, h) = ToBgr(anyFrame);
                    // 아주 가벼운 호출 몇 번로 워밍업
                    for (int i = 0; i < 3; i++)
                    {
                        var det = FaceSDK.Instance().DetectFace(bgr, w, h, use_continuous_img_detect: true);
                        if (det.IsOk() && det.IsFaceDetected())
                        {
                            var face = det.GetFace();
                            FaceSDK.Instance().CheckMask(bgr, w, h, ref face);
                        }
                    }

                    _preWarmed = true;
                }
                catch
                {
                    /* no-op */
                }
            });
        }

        /*[B] 이미지 URL 전송*/
        public async Task<RecognitionResult> RecognizeAsync(Bitmap frame, ObservableCollection<WaitUser> waitUsers)
        {
            // 재진입 가드
            if (!await _sem.WaitAsync(0))
                return new RecognitionResult { IsSuccess = false, Error = "[FRS] busy" };
            Console.WriteLine("[FRS] >>> RecognizeAsync ENTER");

            try
            {
                // 0) 입력 검증
                if (frame == null) return Fail("[FRS] 입력 프레임 없음");

                // 1) BGR 변환
                var (bgr, w, h) = ToBgr(frame);
                var det = FaceSDK.Instance().DetectFace(bgr, w, h, use_continuous_img_detect: true);
                if (!det.IsOk() || !det.IsFaceDetected())
                    return Fail("[FRS] 얼굴 검출 안됨");

                var face = det.GetFace();

                return new RecognitionResult
                {
                    IsSuccess = true,
                    UserName = "이주희",
                    UserOid = "100000558",
                    Dist = 0.2231f
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FRS] 오류: " + ex.Message);
                return new RecognitionResult { IsSuccess = false, Error = "[FRS] 예외" };
            }
            finally
            {
                _sem.Release();
            }

            static RecognitionResult Fail(string msg) => new()
            {
                IsSuccess = false,
                Error = msg
            };
        }

        private (byte[] bgr, int w, int h) ToBgr(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte[] buffer = new byte[bmp.Width * bmp.Height * 3];
                for (int y = 0; y < bmp.Height; y++)
                {
                    IntPtr src = data.Scan0 + y * data.Stride;
                    Marshal.Copy(src, buffer, y * bmp.Width * 3, bmp.Width * 3);
                }

                return (buffer, bmp.Width, bmp.Height);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        /* //[A] Alchera SDK 사용 얼굴인식 (임베딩 이슈로 사용불가)

        //임계값
        const float DIST_THRESH = FaceSDK.THRESHOLD_FEATURE; // 권장≈0.263
        const float COS_THRESH = 0.45f; // 0.40~0.50에서 조정
        const float MARGIN = 0.03f; // top2 - top1

        // L2 노름
        private static float L2(float[] v)
        {
            double s = 0;
            foreach (var x in v) s += x * x;
            return (float)Math.Sqrt(s);
        }

        // 코사인 유사도
        private static float Cosine(float[] a, float[] b)
        {
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            if (na == 0 || nb == 0) return float.NaN;
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
        }

         public async Task<RecognitionResult> RecognizeAsync(Bitmap frame, ObservableCollection<WaitUser> waitUsers)
        {
            // 재진입 가드
            if (!await _sem.WaitAsync(0))
                return new RecognitionResult { IsSuccess = false, Error = "[FRS] busy" };

            try
            {
                // 0) 입력 검증
                if (frame == null) return Fail("[FRS] 입력 프레임 없음");

                // 1) BGR 변환
                var (bgr, w, h) = ToBgr(frame);

                // 2) 얼굴 검출 (Alchera FaceSDK C# 래퍼)
                var det = FaceSDK.Instance().DetectFace(bgr, w, h, use_continuous_img_detect: true);
                if (!det.IsOk() || !det.IsFaceDetected())
                    return Fail("[FRS] 얼굴 검출 안됨");
                var face = det.GetFace();
                var lm = det.GetFaceLandMark();
                var lm5 = det.GetFaceLandMark5pt();

                // 마스크 여부
                bool isMasked = FaceSDK.Instance().CheckMask(bgr, w, h, ref face).IsFaceMasked();

                // 3) 품질 게이트 (특징 추출용)
                var qual = FaceSDK.Instance().EstimateFaceQuality(bgr, w, h, ref face, ref lm, ref lm5, isMasked);
                float q = qual.GetFaceQualityForFeature(); // 권장 임계: 무마스크/유마스크 별도
                float need = isMasked
                    ? FaceSDK.THRESHOLD_FACE_QUALITY_FOR_FEATURE[1]
                    : FaceSDK.THRESHOLD_FACE_QUALITY_FOR_FEATURE[0];

                if (q < need)
                    return Fail($"[FRS] 품질 부족: {q:F2} < {need:F2}");

                // 4) (옵션) 라이브니스 — 환경 안정 전까지는 주석 또는 우회
                /*bool canLiveness = (w >= FaceSDK.THRESHOLD_LIVENESS_SRC_IMG_W_MIN &&
                                    h >= FaceSDK.THRESHOLD_LIVENESS_SRC_IMG_H_MIN);
                if (canLiveness)
                {
                    var fq = FaceSDK.Instance().GetImgFaceQualityForLiveness(bgr, w, h, ref face);
                    var lv = FaceSDK.Instance().GetImgLiveness(bgr, w, h, ref face);
                    if (fq.IsOk() && lv.IsOk())
                    {
                        if (fq.GetFaceQualityForLiveness() < FaceSDK.THRESHOLD_FACE_QUALITY_FOR_ANTISPOOFING ||
                            lv.GetImgLiveness() < FaceSDK.THRESHOLD_ANTISPOOFING)
                            return Fail("[FRS] 라이브니스 실패");
                    }
                }#1#
                // 5) 임베딩 추출 (C++ Wrapper)
                var box = face.box; // float x,y,w,h
                int bx = Math.Max(0, (int)box.x);
                int by = Math.Max(0, (int)box.y);
                int bw = Math.Max(1, Math.Min(w - bx, (int)box.w));
                int bh = Math.Max(1, Math.Min(h - by, (int)box.h));

                float[] camera = new float[FaceSdkWrapper.Dim];
                SdkError err;
                bool ok = FaceSdkWrapper.ExtractFeatureBgr(bgr, w, h, 0, 0, camera, out err);
                if (!ok) return Fail($"[FRS] 임베딩 추출 실패 err={err}");

                // === A) Self-match / L2 / Cosine 실험 로그 ===
                float selfDist = FaceSdkWrapper.MatchFeature(camera, camera);
                float selfCos = Cosine(camera, camera);

                // 6) 대기 사용자 임베딩 수집 (필요 시)
                var gallery = new List<float[]>();
                var galleryUsers = new List<WaitUser>();
                if (waitUsers != null)
                {
                    foreach (var u in waitUsers)
                    {
                        if (string.IsNullOrWhiteSpace(u.Landmark)) continue;
                        // 프로젝트 내 임베딩 파서 사용
                        var vec = EmbeddingParser.ParseEmbedding(u.Landmark);
                        if (vec != null && vec.Length == FaceSdkWrapper.Dim)
                        {
                            gallery.Add(vec);
                            galleryUsers.Add(u);
                        }
                    }
                }

                // 6) 등록 벡터 분포 간단 체크
                if (gallery.Count > 0)
                {
                    var g0 = gallery[0];
                }

                // 7) 1:N (N→1) 빠른 1차 후보 탐색
                int topIndex = -1;
                float topDist = float.MaxValue;

                if (gallery.Count > 0)
                {
                    using (var holder = FloatPtrArray.Build(gallery)) // float** 빌더
                    {
                        topIndex = FaceSdkWrapper.MatchFeatureNto1(camera, holder.Pointer, gallery.Count, out topDist);
                    }
                    float topCosQuick = (topIndex >= 0) ? Cosine(camera, gallery[topIndex]) : float.NaN;
                }

                // 8) 최종 판정(재계산 + 로그)
                // ── 튜닝 파라미터(이름 고정: 한 곳만 손대면 됨)
                float firstDist = topDist;
                float secondDist = float.PositiveInfinity;
                float topCos = float.NaN;

                // 전원 점수표 (거리/코사인 재계산 + 정렬용 값 수집)
                var scoreboard = new List<(int i, string oid, string name, float dist, float cos)>(gallery.Count);

                for (int i = 0; i < gallery.Count; i++)
                {
                    float di = FaceSdkWrapper.MatchFeature(camera, gallery[i]);
                    float ci = Cosine(camera, gallery[i]);

                    var u = galleryUsers[i]; // 기존 idxMap
                    scoreboard.Add((i, u.UserOid ?? "-", u.Name ?? "-", di, ci));

                    if (i != topIndex && di < secondDist) secondDist = di;
                    if (i == topIndex) topCos = ci;
                }

                // 최종 통과 조건: 거리 + 코사인 + 마진
                bool isMatch =
                    (topIndex >= 0) &&
                    (firstDist < DIST_THRESH) &&
                    (topCos > COS_THRESH) &&
                    ((secondDist - firstDist) > MARGIN);

                if (isMatch)
                {
                    var m = galleryUsers[topIndex];
                    return new RecognitionResult
                    {
                        IsSuccess = true,
                        UserName = m.Name,
                        UserOid = m.UserOid,
                        Dist = firstDist
                    };
                }

                return new RecognitionResult
                {
                    IsSuccess = false,
                    Error = $"[FRS] 매칭 실패 (d1={firstDist:F4}, cos1={topCos:F3}, d2-d1={(secondDist - firstDist):F3})",
                    Dist = firstDist
                };
            }
            catch (Exception ex)
            {
                return new RecognitionResult { IsSuccess = false, Error = "[FRS] 예외" };
            }
            finally
            {
                _sem.Release();
            }

            static RecognitionResult Fail(string msg) => new()
            {
                IsSuccess = false,
                Error = msg
            };
        }*/
    }
}