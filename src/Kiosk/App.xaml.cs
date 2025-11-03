using Kiosk.Pages;
using Kiosk.Services;
using Kiosk.Services.Interface;
using Kiosk.ViewModels;
using Kiosk.FaceEngine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Kiosk.Commands;

namespace Kiosk
{
    public partial class App : Application
    {
        public static IConfiguration Configuration { get; private set; }
        public static ILoggerFactory LoggerFactory { get; private set; }
        public static ILogger<App> Logger { get; private set; }

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        protected override void OnStartup(StartupEventArgs e)
        {
            Environment.SetEnvironmentVariable("OPENCV_LOG_LEVEL", "ERROR", EnvironmentVariableTarget.Process);
            base.OnStartup(e);

            AllocConsole();
            Console.WriteLine("애플리케이션이 시작되었습니다.");

            // 로그 파일 경로 설정
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logPath);
            var logFile = Path.Combine(logPath, $"logfile_{DateTime.Now:yyyyMMdd}.log");
            Console.WriteLine($"로그 파일 경로: {logFile}");

            // 구성 설정
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // LoggerFactory 초기화
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
                builder.AddDebug();
                builder.AddFile(logFile);
                builder.AddConfiguration(Configuration.GetSection("Logging"));
            });

            Logger = LoggerFactory.CreateLogger<App>();

            // DI 서비스 등록
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Configuration);
            services.AddLogging(logging => logging.AddProvider(new FileLoggerProvider(logFile)));

            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISoapClient, SoapClient>();
            services.AddSingleton<IBeaconPublisher, BeaconPublisher>();
            services.AddSingleton<FirebaseService>();
            services.AddSingleton<ILocationService, LocationService>();
            services.AddSingleton<AlcheraSDKService>();
            services.AddSingleton<ICameraService, CameraService>();
            services.AddSingleton<IFaceRecognitionService, FaceRecognitionService>();
            services.AddSingleton<IPunchStateCache, PunchStateCache>();
            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddTransient<LoginService>();
            services.AddTransient<IFacialResultService, FacialResultService>();
            services.AddTransient<ICheckPhoneService, CheckPhoneService>();
            services.AddTransient<ICommuteService, CommuteService>();
            services.AddTransient<IImageUrlService, ImageUrlService>();

            services.AddTransient<MainWindow>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LocationSelectionViewModel>();
            services.AddTransient<FaceRecognitionViewModel>();
            services.AddTransient<CheckPhoneViewModel>();
            services.AddTransient<CommuteCheckViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<SectionStaffViewModel>();
            services.AddTransient<SectionCommuteViewModel>();

            services.AddTransient<LoginPage>();
            services.AddTransient<LocationSelectionPage>();
            services.AddTransient<FaceRecognitionPage>();
            services.AddTransient<CheckPhonePage>();
            services.AddTransient<CommuteCheckPage>();
            services.AddTransient<SettingsPage>();

            services.AddTransient(typeof(VmDeps<>), typeof(VmDeps<>));

            var serviceProvider = services.BuildServiceProvider();
            Logger.LogInformation("애플리케이션 초기화 완료");

            //SDK 초기화
            var baseDir = AppContext.BaseDirectory;
            var sdkDir = baseDir; // SDK.dll이 있는 경로
            var modelDir = Path.Combine(baseDir, "models");

            if (!FaceSdkWrapper.InitializeSdk(modelDir, sdkDir))
            {
                MessageBox.Show("SdkWrapper 초기화 실패", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            Logger.LogInformation("SdkWrapper initialized.");

            // SDK 버전 출력
            string version = FaceSdkWrapper.GetSdkVersionString();
            Logger.LogInformation($"[FRS] SDKVersion={version}");
            // 모델 파일 목록 출력
            foreach (var f in Directory.GetFiles(modelDir))
            {
                var fi = new FileInfo(f);
                //Console.WriteLine($"[FRS][model] {fi.Name}  {fi.Length} bytes");
            }

            var rst = FaceSDK.Instance().Initialize(modelDir, sdkDir);
            if (!rst.IsOk())
            {
                MessageBox.Show($"SDK(C#) 초기화 실패: {rst.GetLastErr()} {rst.GetLastErrDesc()}");
                Shutdown();
                return;
            }

            Logger.LogInformation("SDK initialized.");
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                FaceSdkWrapper.FinalizeSdk();
            }
            catch
            {
            }

            Console.WriteLine(" 애플리케이션이 종료됩니다");
            base.OnExit(e);
        }
    }

    public static class LoggingBuilderExtensions
    {
        public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string filePath)
        {
            builder.AddProvider(new FileLoggerProvider(filePath));
            return builder;
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;

        public FileLoggerProvider(string filePath)
        {
            _filePath = filePath;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_filePath);
        }

        public void Dispose()
        {
        }
    }

    public class FileLogger : ILogger
    {
        private readonly string _filePath;
        private static readonly object _lock = new object();

        public FileLogger(string filePath)
        {
            _filePath = filePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {message}";
            if (exception != null)
                logMessage += $"\nException: {exception}";

            lock (_lock)
            {
                File.AppendAllText(_filePath, logMessage + Environment.NewLine);
            }
        }
    }
}