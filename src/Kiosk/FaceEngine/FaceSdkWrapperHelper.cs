using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.FaceEngine
{
    public static class FaceSdkWrapperHelper
    {
        /// <summary>
        /// 현재 사용자 얼굴 특징 벡터 추출 (예: 카메라/이미지로부터)
        /// </summary>
        public static float[] ExtractTargetFeature()
        {
            // 실제 구현 시 이미지 → ExtractFeature 사용
            float[] feature = new float[512];
            for (int i = 0; i < 512; i++) feature[i] = 0.01f * i;
            return feature;
        }

        /// <summary>
        /// 등록된 사용자 얼굴 벡터들 불러오기 (예: DB에서)
        /// </summary>
        public static float[][] LoadRegisteredFeatureArray()
        {
            int num_users = 3;
            float[][] features = new float[num_users][];
            for (int i = 0; i < num_users; i++)
            {
                features[i] = new float[512];
                for (int j = 0; j < 512; j++) features[i][j] = 0.01f * j + 0.001f * i;
            }
            return features;
        }

        /// <summary>
        /// N:1 매칭 수행 (features는 등록된 얼굴 벡터 목록)
        /// </summary>
        
        public static int MatchNto1(float[] target, float[][] features, out float minDistance)
        {
            if (target == null || target.Length != 512)
                throw new ArgumentException("target must be length 512.");
            if (features == null || features.Length == 0)
                throw new ArgumentException("features must be non-empty.");

            foreach (var f in features)
                if (f == null || f.Length != 512)
                    throw new ArgumentException("each feature must be length 512.");

            int numFeats = features.Length;
            int floatSize = sizeof(float);
            int ptrSize = IntPtr.Size;

            IntPtr ptrArray = IntPtr.Zero;           // float** (포인터 배열)
            var nativePtrs = new IntPtr[numFeats];   // 각 float[512] 블록 주소

            try
            {
                // 포인터 배열 공간
                ptrArray = Marshal.AllocHGlobal(ptrSize * numFeats);

                for (int i = 0; i < numFeats; i++)
                {
                    nativePtrs[i] = Marshal.AllocHGlobal(512 * floatSize);
                    Marshal.Copy(features[i], 0, nativePtrs[i], 512);
                }

                // IntPtr[] -> 네이티브 메모리로 복사
                Marshal.Copy(nativePtrs, 0, ptrArray, numFeats);

                // 호출
                return FaceSdkWrapper.MatchFeatureNto1(target, ptrArray, numFeats, out minDistance);
            }
            finally
            {
                // 해제
                for (int i = 0; i < nativePtrs.Length; i++)
                    if (nativePtrs[i] != IntPtr.Zero) Marshal.FreeHGlobal(nativePtrs[i]);
                if (ptrArray != IntPtr.Zero) Marshal.FreeHGlobal(ptrArray);
            }
        }
    }
}


//public static int MatchNto1(float[] target, float[][] features, out float minDistance)
//{
//    int numFeats = features.Length;
//    int floatSize = sizeof(float);
//    int ptrSize = Marshal.SizeOf(typeof(IntPtr));

//    // 포인터 배열용 네이티브 메모리 할당
//    IntPtr buffer = Marshal.AllocHGlobal(ptrSize * numFeats);
//    IntPtr[] nativePtrs = new IntPtr[numFeats];

//    try
//    {
//        for (int i = 0; i < numFeats; i++)
//        {
//            nativePtrs[i] = Marshal.AllocHGlobal(512 * floatSize);
//            Marshal.Copy(features[i], 0, nativePtrs[i], 512);
//        }

//        // 포인터 배열 복사
//        Marshal.Copy(nativePtrs, 0, buffer, numFeats);

//        // C++ SDK 호출
//        return FaceSdkWrapper.MatchFeatureNto1(target, buffer, numFeats, out minDistance);
//    }
//    finally
//    {
//        // 메모리 해제 (예외 발생하더라도 보장)
//        for (int i = 0; i < numFeats; i++)
//        {
//            if (nativePtrs[i] != IntPtr.Zero)
//                Marshal.FreeHGlobal(nativePtrs[i]);
//        }
//        Marshal.FreeHGlobal(buffer);
//    }
//}