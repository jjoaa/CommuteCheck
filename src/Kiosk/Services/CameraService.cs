using Kiosk.FaceEngine;
using Kiosk.Services.Interface;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiosk.Services
{
    public class CameraService : ICameraService, IDisposable
    {
        private readonly object _capLock = new(); // 모든 카메라 접근 동기화
        private VideoCapture? _capture;
        private bool _isRunning;
        private bool _isOpened;

        private Task? _workerTask;
        private CancellationTokenSource? _cts;
        public event EventHandler<Bitmap> FrameCaptured;

        // 프레임 이벤트 최소 간격(디바운스/샘플링)
        private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(200);
        private DateTime _lastEmitted = DateTime.MinValue;

        private const bool MirrorHorizontal = true; // 수평미러

        // 품질 선별용
        private volatile Bitmap? _bestShot;
        private double _bestScore = 0;
        private readonly object _bestLock = new();

        // 베스트샷 유효 기간(너무 오래된 컷 방지)
        private readonly TimeSpan _bestShotTtl = TimeSpan.FromSeconds(8);
        private DateTime _bestShotAt = DateTime.MinValue;

        /*public Task OpenCameraAsync()
        {
            if (_isRunning) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _workerTask = Task.Run(() =>
            {
                Console.WriteLine("[CameraService] 카메라 열기 시도");
                _capture = new VideoCapture(0, VideoCaptureAPIs.MSMF); //벡엔드 명시
                //_capture = new VideoCapture(0);
                _capture.Set(VideoCaptureProperties.FrameWidth, 1440);
                _capture.Set(VideoCaptureProperties.FrameHeight, 1920);
                _capture.Set(VideoCaptureProperties.Fps, 30);

                // 실제 적용 값 로깅
                Console.WriteLine($"[Cam] w={_capture.Get(VideoCaptureProperties.FrameWidth)}, " +
                                  $"h={_capture.Get(VideoCaptureProperties.FrameHeight)}, " +
                                  $"fps={_capture.Get(VideoCaptureProperties.Fps)}");

                if (!_capture.IsOpened())
                    throw new Exception("카메라를 열 수 없습니다.");

                Console.WriteLine("[CameraService] 카메라 열림");

                _isRunning = true;
                using var frame = new Mat();
                int frameCounter = 0;

                try
                {
                    while (_isRunning && !ct.IsCancellationRequested)
                    {
                        _capture.Read(frame);
                        if (!frame.Empty())
                        {
                            frameCounter++;
                            if (frameCounter % 60 == 0)
                                Console.WriteLine("[CameraService] 프레임 읽음");

                            // ← 추가: 최소 간격 체크(200ms)
                            var now = DateTime.UtcNow;
                            if (now - _lastEmitted < _minInterval)
                                continue;
                            _lastEmitted = now;

                            Cv2.Flip(frame, frame, FlipMode.Y);
                            var bitmap = BitmapConverter.ToBitmap(frame);
                            FrameCaptured?.Invoke(this, bitmap);
                        }
                    }
                }
                finally
                {
                    _isRunning = false;
                    // 루프 종료 후에 해제/Dispose (안전)
                    /*_capture?.Release();
                    _capture?.Dispose();#1#
                    var cap = Interlocked.Exchange(ref _capture, null);
                    if (cap != null)
                    {
                        try
                        {
                            cap.Release();
                        }
                        catch (ObjectDisposedException)
                        {
                        }

                        cap.Dispose();
                    }

                    Console.WriteLine("[CameraService] 카메라 루프 종료");
                }
            }, ct);

            return Task.CompletedTask;
        }
        */
        /*
        public void CloseCamera()
        {
            _isRunning = false;
            _capture?.Release();
            _capture?.Dispose();
        }
        */
        public Task OpenCameraAsync()
        {
            if (_isRunning) return Task.CompletedTask;

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            _workerTask = Task.Run(() =>
            {
                Console.WriteLine("[CameraService] 카메라 열기 시도");

                lock (_capLock)
                {
                    _capture = new VideoCapture(0, VideoCaptureAPIs.MSMF);
                    _capture.Set(VideoCaptureProperties.FrameWidth, 1440);
                    _capture.Set(VideoCaptureProperties.FrameHeight, 1920);
                    _capture.Set(VideoCaptureProperties.Fps, 30);

                    if (!_capture.IsOpened())
                    {
                        _capture.Release();
                        _capture.Dispose();
                        _capture = null;
                        throw new Exception("카메라를 열 수 없습니다.");
                    }

                    _isOpened = true;

                    Console.WriteLine($"[Cam] w={_capture.Get(VideoCaptureProperties.FrameWidth)}, " +
                                      $"h={_capture.Get(VideoCaptureProperties.FrameHeight)}, " +
                                      $"fps={_capture.Get(VideoCaptureProperties.Fps)}");
                }

                Console.WriteLine("[CameraService] 카메라 열림");

                _isRunning = true;
                int frameCounter = 0;

                try
                {
                    while (_isRunning && !ct.IsCancellationRequested)
                    {
                        // 최소 간격 체크
                        var now = DateTime.UtcNow;
                        if (now - _lastEmitted < _minInterval)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        Mat? mat = null;
                        try
                        {
                            lock (_capLock)
                            {
                                if (!_isOpened || _capture is null) break;

                                // Grab/Retrieve 2단계 사용: 안정적
                                if (!_capture.Grab())
                                    continue;

                                mat = new Mat();
                                _capture.Retrieve(mat);
                                if (mat.Empty())
                                {
                                    mat.Dispose();
                                    mat = null;
                                    continue;
                                }
                            }

                            frameCounter++;
                            if (frameCounter % 60 == 0)
                                Console.WriteLine("[CameraService] 프레임 읽음");

                            if (MirrorHorizontal)
                                Cv2.Flip(mat, mat, FlipMode.Y);

                            using var bmp = BitmapConverter.ToBitmap(mat); // 지역에서 생성
                            // 소비자 측에서 오래 들고 있어도 안전하도록 Clone() 전달
                            var outbound = (Bitmap)bmp.Clone();

                            _lastEmitted = now;
                            FrameCaptured?.Invoke(this, outbound);
                            // outbound는 수신자 책임으로 Dispose (이벤트 사용 측에서)
                        }
                        finally
                        {
                            mat?.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[CameraService] 루프 예외: " + ex);
                }
                finally
                {
                    _isRunning = false;

                    lock (_capLock)
                    {
                        _isOpened = false;
                        if (_capture != null)
                        {
                            try
                            {
                                _capture.Release();
                            }
                            catch
                            {
                                /* no-op */
                            }

                            _capture.Dispose();
                            _capture = null;
                        }
                    }

                    Console.WriteLine("[CameraService] 카메라 루프 종료");
                }
            }, ct);

            return Task.CompletedTask;
        }

        public async Task CloseCameraAsync()
        {
            _isRunning = false;
            _cts?.Cancel(); // 루프에 취소 신호
            if (_workerTask != null)
            {
                try
                {
                    await _workerTask.ConfigureAwait(false);
                }
                catch
                {
                    /* 무시 또는 로깅 */
                }

                _workerTask = null;
            }

            _cts?.Dispose();
            _cts = null;
            /*var cap = Interlocked.Exchange(ref _capture, null);
            if (cap != null)
            {
                try
                {
                    cap.Release();
                }
                catch (ObjectDisposedException)
                {
                }

                cap.Dispose();
            }*/
            lock (_capLock)
            {
                _isOpened = false;
                if (_capture != null)
                {
                    try
                    {
                        _capture.Release();
                    }
                    catch
                    {
                        /* no-op */
                    }

                    _capture.Dispose();
                    _capture = null;
                }
            }
        }

        // 단발 캡쳐도 루프와 동일한 락으로 보호 (동시 Read 금지)
        public async Task<Bitmap?> CaptureFrameAsync(CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                lock (_capLock)
                {
                    if (!_isOpened || _capture is null) return null;

                    try
                    {
                        if (!_capture.Grab()) return null;

                        using var mat = new Mat();
                        _capture.Retrieve(mat);
                        if (mat.Empty()) return null;

                        if (MirrorHorizontal)
                            Cv2.Flip(mat, mat, FlipMode.Y);

                        using var bmp = BitmapConverter.ToBitmap(mat);
                        return (Bitmap)bmp.Clone(); // 호출자에게 안전한 복제본 반환
                    }
                    catch (ObjectDisposedException)
                    {
                        return null;
                    }
                }
            }, ct);
        }

        // IPullCamera 인터페이스 구현
        public Task StartAsync() => OpenCameraAsync();
        public Task StopAsync() => CloseCameraAsync();

        public void Dispose()
        {
            CloseCameraAsync().GetAwaiter().GetResult();
        }

        /*public async Task<Bitmap?> CaptureFrameAsync(CancellationToken ct)
        {
            var cap = _capture;
            if (cap == null) return null;

            return await Task.Run(() =>
            {
                try
                {
                    using var frame = new Mat();
                    if (!cap.Read(frame) || frame.Empty()) return null;
                    Cv2.Flip(frame, frame, FlipMode.Y);
                    return BitmapConverter.ToBitmap(frame);
                }
                catch (ObjectDisposedException)
                {
                    return null;
                }
            }, ct);
        }*/
    }
}