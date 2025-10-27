#pragma once
#include <cstdint>

#ifdef FACESDKWRAPPER_EXPORTS
#  define FACESDK_API __declspec(dllexport)
#else
#  define FACESDK_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

    FACESDK_API void  SetOpenCvLogLevel(int level);
    FACESDK_API const char* GetSdkVersion();
    FACESDK_API bool  InitializeSdk(const char* modelPath, const char* sdkPath);
    FACESDK_API void  FinalizeSdk();

    FACESDK_API float MatchFeature(const float* feat1, const float* feat2);

    FACESDK_API int   MatchFeatureNto1(
        const float* target_feat,
        const float** registered_feats,
        int           num_feats,
        float* min_distance 
    );

    FACESDK_API bool  ExtractFeatureBGR(
        const uint8_t* bgr,
        int            width,
        int            height,
        int            rotateDeg,
        int            mirror,
        float* out512,
        int* err
    );

#ifdef __cplusplus
}
#endif