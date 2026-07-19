using System.Runtime.InteropServices;

namespace WinNASTools.Core.Services;

/// <summary>默认播放设备：峰值探测（是否出声）与系统静音。</summary>
public static class SystemAudioControl
{
    /// <summary>短时采样峰值；true=有可闻输出；false=静音/无声；null=探测失败。</summary>
    public static bool? IsOutputAudible(float threshold = 0.015f, int samples = 4, int delayMs = 40)
    {
        IAudioMeterInformation? meter = null;
        try
        {
            if (!TryGetMeter(out meter) || meter is null)
                return null;

            for (var i = 0; i < samples; i++)
            {
                if (meter.GetPeakValue(out var peak) == 0 && peak >= threshold)
                    return true;
                if (i + 1 < samples)
                    Thread.Sleep(delayMs);
            }

            return false;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (meter is not null)
                Marshal.ReleaseComObject(meter);
        }
    }

    public static bool? GetMute()
    {
        IAudioEndpointVolume? vol = null;
        try
        {
            if (!TryGetVolume(out vol) || vol is null)
                return null;
            if (vol.GetMute(out var muted) != 0)
                return null;
            return muted;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (vol is not null)
                Marshal.ReleaseComObject(vol);
        }
    }

    public static bool TrySetMute(bool mute)
    {
        IAudioEndpointVolume? vol = null;
        try
        {
            if (!TryGetVolume(out vol) || vol is null)
                return false;
            return vol.SetMute(mute, Guid.Empty) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (vol is not null)
                Marshal.ReleaseComObject(vol);
        }
    }

    private static bool TryGetMeter(out IAudioMeterInformation? meter)
    {
        meter = null;
        return TryActivate<IAudioMeterInformation>(typeof(IAudioMeterInformation).GUID, out meter);
    }

    private static bool TryGetVolume(out IAudioEndpointVolume? volume)
    {
        volume = null;
        return TryActivate<IAudioEndpointVolume>(typeof(IAudioEndpointVolume).GUID, out volume);
    }

    private static bool TryActivate<T>(Guid iid, out T? iface) where T : class
    {
        iface = null;
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out device) != 0
                || device is null)
                return false;

            var guid = iid;
            if (device.Activate(ref guid, ClsCtxAll, IntPtr.Zero, out var ptr) != 0 || ptr == IntPtr.Zero)
                return false;

            iface = (T)Marshal.GetObjectForIUnknown(ptr);
            Marshal.Release(ptr);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (device is not null) Marshal.ReleaseComObject(device);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }
    }

    private const uint ClsCtxAll = 0x17;

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

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D023F984F57A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float peak);
        [PreserveSig] int GetMeteringChannelCount(out int channelCount);
        [PreserveSig] int GetChannelsPeakValues(int channelCount, [Out] float[] peakValues);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
        [PreserveSig] int GetChannelCount(out uint channelCount);
        [PreserveSig] int SetMasterVolumeLevel(float levelDb, Guid eventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid eventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, Guid eventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
        [PreserveSig] int VolumeStepUp(Guid eventContext);
        [PreserveSig] int VolumeStepDown(Guid eventContext);
        [PreserveSig] int QueryHardwareSupport(out uint hardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float minDb, out float maxDb, out float incrementDb);
    }
}
