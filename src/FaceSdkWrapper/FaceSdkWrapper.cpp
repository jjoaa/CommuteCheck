#define FACESDKWRAPPER_EXPORTS
#include "pch.h"
#include "FaceSdkWrapper.h"

#include "alchera/face_sdk.h"
#include "alchera/types.h"
#include "alchera/errors.h"
#include <algorithm>
#include <array>
#include <vector>
#include <cstdlib>
#include <windows.h>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <shlwapi.h>
#pragma comment(lib, "Shlwapi.lib")

using namespace alchera::FaceSDK;

static alchera::FaceSDK::FaceSDK* g_sdk = nullptr;

/* 공통: Bool 체크 */
static inline bool check_fasdk_result(const alchera::FaceSDK::Bool& ret) {
    return ret.result && ret.last_error == alchera::FaceSDK::Error::NoError;
}
static void logA(const char* s) { OutputDebugStringA(s); OutputDebugStringA("\n"); }

static void log_last_error(const char* prefix) {
    DWORD e = GetLastError();
    char buf[512];
    FormatMessageA(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL, e, 0, buf, sizeof(buf), NULL);
    char line[640]; sprintf_s(line, "%s (GetLastError=%lu) %s", prefix, e, buf);
    logA(line);
}

extern "C" {

    /* OpenCV 전역 로그 레벨 설정 (C#에서도 호출 가능) */
    FACESDK_API void SetOpenCvLogLevel(int level) {
        const char* s = "ERROR";
        switch (level) {
        case 0:s = "SILENT"; break; case 1:s = "FATAL"; break; case 2:s = "ERROR"; break;
        case 3:s = "WARNING"; break; case 4:s = "INFO"; break; case 5:s = "DEBUG"; break; case 6:s = "VERBOSE"; break;
        }
        _putenv_s("OPENCV_LOG_LEVEL", s);
    }
    /*버전 확인*/
    FACESDK_API const char* GetSdkVersion() {
        if (!g_sdk) return "SDK not initialized";
        static std::string v = g_sdk->GetSDKVersion(); 
        return v.c_str();
    }
    /* SDK 초기화 */
    FACESDK_API bool InitializeSdk(const char* modelPath, const char* sdkPath) {
        if (g_sdk) return true;

        logA("[FSW] InitializeSdk enter");

        SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        wchar_t wSdk[MAX_PATH];
        MultiByteToWideChar(CP_ACP, 0, sdkPath, -1, wSdk, MAX_PATH);
        AddDllDirectory(wSdk);

        HMODULE h = LoadLibraryA("AlcheraFaceSDK.dll");
        if (!h) { log_last_error("[FSW] LoadLibraryA(AlcheraFaceSDK.dll) failed"); return false; }
        logA("[FSW] AlcheraFaceSDK.dll loaded OK");

        if (GetFileAttributesA(modelPath) == INVALID_FILE_ATTRIBUTES ||
            GetFileAttributesA(sdkPath) == INVALID_FILE_ATTRIBUTES) {
            logA("[FSW] path invalid");
            return false;
        }

        g_sdk = new alchera::FaceSDK::FaceSDK();
        logA("[FSW] new FaceSDK ok");

        auto init = g_sdk->Initialize(modelPath, sdkPath);
        if (!(init.result && init.last_error == alchera::FaceSDK::Error::NoError)) {
            char b[128];
            sprintf_s(b, "[FSW] Initialize FAILED: last_error=%d", (int)init.last_error);
            logA(b);
            delete g_sdk; g_sdk = nullptr;
            return false;     // ← 여기 반드시 세미콜론 필요!
        }                     // ← 블록 닫기

        auto en = g_sdk->GetFeatureExtension().Enable(true);
        if (!(en.result && en.last_error == alchera::FaceSDK::Error::NoError)) {
            logA("[FSW] FeatureExtension.Enable(true) FAILED");
            delete g_sdk; g_sdk = nullptr;
            return false;
        }

        auto isEn = g_sdk->GetFeatureExtension().IsEnabled();
        if (!(isEn.result)) {
            logA("[FSW] FeatureExtension not enabled");
            delete g_sdk; g_sdk = nullptr;
            return false;
        }
        logA("[FSW] Initialize OK");
        return true;
    }

    /* 종료 */
    FACESDK_API void FinalizeSdk() {
        if (g_sdk) { delete g_sdk; g_sdk = nullptr; }
    }

    /*얼굴 특징 벡터 간 유사도 비교*/
    FACESDK_API float MatchFeature(const float* feat1, const float* feat2) {
        // 1. 두 feature 벡터를 std::array로 래핑
        std::array<float, 512> v1, v2;
        std::copy(feat1, feat1 + 512, v1.begin());
        std::copy(feat2, feat2 + 512, v2.begin());

        // 2. 거리 계산
        auto d = g_sdk->GetFeatureExtension().ComputeDistance(v1, v2);

        // 3. 에러 체크
        if (d.last_error != Error::NoError) return -1.0f; // 오류 시 음수 반환

        // 4. 유사도 반환 (작을수록 비슷)
        return d.distance;
    }

    /* 1:N 최솟값 인덱스/거리 */
    FACESDK_API int MatchFeatureNto1(
        const float* target_feat,
        const float** registered_feats,
        int           num_feats,
        float* min_distance) {
        std::array<float, 512> target;
        std::copy(target_feat, target_feat + 512, target.begin());

        std::vector<std::array<float, 512>> regs(num_feats);
        for (int i = 0; i < num_feats; ++i) {
            std::copy(registered_feats[i], registered_feats[i] + 512, regs[i].begin());
        }

        FeatureExtension::InputFaceFeatures in(target, regs);
        auto r = g_sdk->GetFeatureExtension().ComputeDistances(in);
        if (r.last_error != Error::NoError || !r.distances) return -1;

        *min_distance = r.distances->at(r.minimum_distance_index);
        return r.minimum_distance_index;
    }
#if defined(_DEBUG)
#pragma comment(lib, "opencv_world455.lib")
#else
#pragma comment(lib, "opencv_world455.lib")
#endif

    /* 얼굴 특징 추출 (+ BGR 입력, 회전/미러) */
    FACESDK_API bool ExtractFeatureBGR(
        const uint8_t* bgr, int width, int height,
        int rotateDeg, int mirror,
        float* out512, int* err) {

        //logA("[FSW] >>> Enter ExtractFeatureBGR");   // 함수 진입

        if (err) *err = 0;
        if (!g_sdk) { if (err)*err = -100; return false; }
        if (!bgr || !out512) { if (err)*err = -1;   return false; }
        if (width <= 0 || height <= 0) { if (err)*err = -2; return false; }


        // 1) 전처리 OpenCV 뷰 구성 (복사 없이) 
        cv::Mat src(height, width, CV_8UC3, const_cast<uint8_t*>(bgr));
        cv::Mat proc = src;

        // 2) 전처리 회전
        switch (rotateDeg) {
        case 90:  cv::rotate(proc, proc, cv::ROTATE_90_CLOCKWISE); break;
        case 180: cv::rotate(proc, proc, cv::ROTATE_180); break;
        case 270: cv::rotate(proc, proc, cv::ROTATE_90_COUNTERCLOCKWISE); break;
        default:  /* 0도면 그대로 */
            break;
        }
        // 3) 전처리 좌우반전 (미러)
        if (mirror) cv::flip(proc, proc, 1);
        // A) 오리엔트/크기 로그
        {
            char dbg[160];
            sprintf_s(dbg, "[FSW] orient r=%d mir=%d  src=%dx%d  proc=%dx%d",
                rotateDeg, mirror, width, height, proc.cols, proc.rows);
            logA(dbg);
        } 
        /*연속 메모리 보장*/
        cv::Mat cont = proc.isContinuous() ? proc : proc.clone();
        
        InputImage image{};
        image.bgr_image_buffer = cont.data;
        image.width = static_cast<std::size_t>(cont.cols);
        image.height = static_cast<std::size_t>(cont.rows);
        /*InputImage image{};
        image.bgr_image_buffer = proc.data;
        image.width = static_cast<std::size_t>(proc.cols);
        image.height = static_cast<std::size_t>(proc.rows);*/
        logA("[FSW] InputImage set OK");
        // 5) 얼굴 추출
        alchera::FaceSDK::Faces detected = g_sdk->DetectFaceInSingleImage(image);

        if (detected.faces.empty()) {
            if (err) *err = -3;
            logA("[FSW] No face detected");
            return false;
        }
        // 6) 가장 큰 얼굴 추출
        size_t pick = 0;
        double bestArea = -1.0;

        for (size_t i = 0; i < detected.faces.size(); ++i) {
            const auto& f = detected.faces[i];   // 반드시 즉시 초기화된 참조

            double area = static_cast<double>(f.box.width) * static_cast<double>(f.box.height);

            char dbg[160];
            sprintf_s(dbg, "[FSW] face[%Iu] box=(%.1f,%.1f,%.1f,%.1f) area=%.1f",
                (unsigned)i, f.box.x, f.box.y, f.box.width, f.box.height, area);
            logA(dbg);

            if (area > bestArea) {
                bestArea = area;
                pick = i;
            }
        }

        const auto& face = detected.faces[pick];
        logA("[FSW] Picked largest face");
        // 간단버전 : 첫 얼굴 사용
        //const auto& face = detected.faces[0];
        
        //랜드마크 신뢰도 
        auto conf = g_sdk->ComputeLandmarkConfidence(image, face);
        if (conf.last_error != Error::NoError) { if (err)*err = -4; return false; }
        if (conf.confidence < 0.60f) { if (err)*err = -5; return false; }

        //  7) Feature 추출
        auto feat = g_sdk->GetFeatureExtension().ExtractFeature(image, face);
        if (feat.last_error != alchera::FaceSDK::Error::NoError) { if (err) *err = -6; return false; }

        // 8) 복사
        std::copy(feat.feature_vector.begin(), feat.feature_vector.end(), out512);
        return true;
    }

    ///* 얼굴 특징 추출 (+ BGR 입력만, 전처리X) */
    //FACESDK_API bool ExtractFeatureBGR(
    //    const uint8_t* bgr, int width, int height,
    //    int rotateDeg, int mirror,
    //    float* out512, int* err) {

    //    //logA("[FSW] >>> Enter ExtractFeatureBGR");   // 함수 진입

    //    if (err) *err = 0;
    //    if (!g_sdk) { if (err)*err = -100; return false; }
    //    if (!bgr || !out512) { if (err)*err = -1;   return false; }
    //    if (width <= 0 || height <= 0) { if (err)*err = -2; return false; }

    //    // 1) InputImage 구성
    //    InputImage image{};
    //    image.bgr_image_buffer = const_cast<uint8_t*>(bgr);
    //    image.width = static_cast<std::size_t>(width);
    //    image.height = static_cast<std::size_t>(height);
    //    logA("[FSW] InputImage set OK");
    //    // 2) 얼굴 추출
    //    alchera::FaceSDK::Faces detected = g_sdk->DetectFaceInSingleImage(image);

    //    if (detected.faces.empty()) {
    //        if (err) *err = -3;
    //        logA("[FSW] No face detected");
    //        return false;
    //    }
    //    // 3) 가장 큰 얼굴 추출
    //    size_t pick = 0;
    //    double bestArea = -1.0;

    //    for (size_t i = 0; i < detected.faces.size(); ++i) {
    //        const auto& f = detected.faces[i];   // 반드시 즉시 초기화된 참조

    //        double a = static_cast<double>(f.box.width) * static_cast<double>(f.box.height);

    //        // 로그 찍기 (선택 사항)
    //        char buf[160];
    //        sprintf_s(buf, "[FSW] face[%zu] box=(%.1f,%.1f,%.1f,%.1f) area=%.1f",
    //            i, f.box.x, f.box.y, f.box.width, f.box.height, a);
    //        logA(buf);

    //        if (a > bestArea) {
    //            bestArea = a;
    //            pick = i;
    //        }
    //    }

    //    const auto& face = detected.faces[pick];
    //    logA("[FSW] Picked largest face");
    //    // 간단버전 : 첫 얼굴 사용
    //    //const auto& face = detected.faces[0]; 

    //    //  4) Feature 추출
    //    auto feat = g_sdk->GetFeatureExtension().ExtractFeature(image, face);
    //    if (feat.last_error != alchera::FaceSDK::Error::NoError) { if (err) *err = -6; return false; }

    //    // 5) 복사
    //    std::copy(feat.feature_vector.begin(), feat.feature_vector.end(), out512);
    //    return true;     
    // 
    //}
}
