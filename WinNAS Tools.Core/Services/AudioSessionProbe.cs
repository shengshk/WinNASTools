using System.Runtime.InteropServices;

namespace WinNASTools.Core.Services;

/// <summary>
/// 探测系统默认输出设备上是否存在“正在播放”的音频会话。
/// 基于 Windows Core Audio（WASAPI 会话枚举），无需第三方依赖。
/// </summary>
public static class AudioSessionProbe
{
    /// <summary>true=有音频正在播放；false=没有；null=探测失败（调用方按保守策略处理）。</summary>
    public static bool? IsAnyAudioPlaying()
    {
        IMMDeviceEnumerator? deviceEnumerator = null;
        try
        {
            deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device) != 0
                || device is null)
                return null;

            try
            {
                var iid = typeof(IAudioSessionManager2).GUID;
                if (device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out var mgrPtr) != 0 || mgrPtr == IntPtr.Zero)
                    return null;

                var manager = (IAudioSessionManager2)Marshal.GetObjectForIUnknown(mgrPtr);
                Marshal.Release(mgrPtr);
                try
                {
                    if (manager.GetSessionEnumerator(out var sessions) != 0 || sessions is null)
                        return null;

                    try
                    {
                        if (sessions.GetCount(out var count) != 0)
                            return null;

                        for (var i = 0; i < count; i++)
                        {
                            if (sessions.GetSession(i, out var control) != 0 || control is null)
                                continue;
                            try
                            {
                                if (control.GetState(out var state) == 0 && state == AudioSessionState.Active)
                                    return true;
                            }
                            finally { Marshal.ReleaseComObject(control); }
                        }

                        return false;
                    }
                    finally { Marshal.ReleaseComObject(sessions); }
                }
                finally { Marshal.ReleaseComObject(manager); }
            }
            finally { Marshal.ReleaseComObject(device); }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (deviceEnumerator is not null)
                Marshal.ReleaseComObject(deviceEnumerator);
        }
    }

    private const uint ClsCtxAll = 0x17;

    private enum AudioSessionState { Inactive = 0, Active = 1, Expired = 2 }

    private enum EDataFlow { Render = 0, Capture = 1, All = 2 }

    private enum ERole { Console = 0, Multimedia = 1, Communications = 2 }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, out IntPtr iface);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr props);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, uint streamFlags, out IntPtr sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, uint streamFlags, out IntPtr simpleVolume);
        // IAudioSessionManager2
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification(IntPtr sessionId, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        // 仅使用第一个方法 GetState；其余方法未声明，因为不会调用。
        [PreserveSig] int GetState(out AudioSessionState state);
    }
}
