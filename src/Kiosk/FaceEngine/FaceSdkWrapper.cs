using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Kiosk.Models;

namespace Kiosk.FaceEngine
{
    public static class FaceSdkWrapper
    {
        private const string Dll = "FaceSdkWrapper.dll";
        public const int Dim = 512;

        /* SDK 초기화 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool InitializeSdk(string modelPath, string sdkPath);

        /* 종료 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void FinalizeSdk();

        /* SDK 버전 정보 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr GetSdkVersion();

        /* SDK 버전 정보를 문자열로 반환하는 헬퍼 메서드 */
        public static string GetSdkVersionString()
        {
            IntPtr ptr = GetSdkVersion();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "Unknown" : "Unknown";
        }

        /* 얼굴 개수 검출: 반환은 int라 그대로 둬도 됨 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DetectFace(byte[] imageBuf, int width, int height);

        /* 단일 얼굴 특징 추출 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ExtractFeature(byte[] imageBuf, int width, int height,
            int x, int y, int w, int h,
            float[] lm_xy, int lm_len,
            [Out] float[] out512);

        [DllImport(Dll, EntryPoint = "ExtractFeature", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool ExtractFeatureBgr(
            byte[] bgr, int width, int height,
            int rotateDeg, int mirror,
            [Out] float[] out512,
            out SdkError err);


        /* 자동 특징 추출 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool ExtractFeatureAuto(
            byte[] bgr, int w, int h,
            [Out] float[] out512,
            out int out_error);

        /* 두 벡터 거리 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern float MatchFeature([In] float[] feat1, [In] float[] feat2);

        /* 1:N 최솟값 인덱스/거리 */
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int MatchFeatureNto1(
            [In] float[] target_feat,
            IntPtr registered_feats, // float*[N]
            int num_feats,
            out float min_distance);

        // 단일 얼굴: bbox + (옵션) 랜드마크
        //[DllImport("FaceSdkWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern bool ExtractFeature(
        //    byte[] imageBuf, int width, int height,
        //    int x, int y, int w, int h,
        //    float[] lm_xy, int lm_len,
        //    [Out] float[] out512);

        //[DllImport("FaceSdkWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern bool ExtractFeatureAuto(byte[] bgr, int w, int h, [Out] float[] out512, out int err);

        //// 두 벡터 거리
        //[DllImport("FaceSdkWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern float MatchFeature([In] float[] a, [In] float[] b);

        //// 1:N (필요 시 사용)
        //[DllImport("FaceSdkWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern int MatchFeatureNto1(
        //    float[] target_feat, IntPtr registered_feats /* float** */,
        //    int num_feats, out float min_distance);

        //[DllImport("FaceSdkWrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        //public static extern void SetOpenCvLogLevel(int level);
    }
}