using System.Management;
using Microsoft.Win32;

namespace Kiosk.Utils;

public sealed class DeviceInfo
{
    public required string App { get; init; } // app 모드("0"/"1")
    public required string DeviceOs { get; init; } // 예: "10"
    public required string DeviceModel { get; init; } // 예: "16Z90S"
    public required string DeviceId { get; init; } // MachineGuid
}

public static class EnvironmentHelper
{
    private static DeviceInfo? _cache;

    public static DeviceInfo BuildDeviceInfo(string app)
    {
        if (_cache != null && _cache.App == app) return _cache;

        var os = GetWindowsMajorVersion();
        var model = GetMachineModel();
        var id = GetDeviceId();

        _cache = new DeviceInfo
        {
            App = app,
            DeviceOs = "Window " + os,
            DeviceModel = model,
            DeviceId = id
        };
        return _cache;
    }

    public static string GetWindowsMajorVersion()
    {
        try
        {
            return Environment.OSVersion.Version.Major.ToString();
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    public static string GetMachineModel()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (var mo in searcher.Get())
                return (mo["Model"]?.ToString() ?? "UNKNOWN").Split('-')[0];
        }
        catch
        {
            return "UNKNOWN";
        }

        // 루프에 아무 값도 없었을 경우 대비
        return "UNKNOWN";
    }

    public static string GetDeviceId()
    {
        try
        {
            return Microsoft.Win32.Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography",
                "MachineGuid", ""
            )?.ToString() ?? Guid.NewGuid().ToString();
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }
}