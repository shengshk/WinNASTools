using System.Runtime.InteropServices;

namespace WinNASTools.Core.Native;

internal static class NativeMethods
{
    private const uint Th32csSnapprocess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public const int SwMinimize = 6;
    public const int SwRestore = 9;
    public const int SwShowMaximized = 3;
    public const int GwlExstyle = -20;
    public const int WsExToolwindow = 0x00000080;
    public const int WsExTopmost = 0x00000008;

    public const int WmAppcommand = 0x0319;
    public const int WmClose = 0x0010;
    public const int AppcommandMediaStop = 13;
    public const int AppcommandMediaPlayPause = 14;
    public const int AppcommandMediaPlay = 46;
    public const int AppcommandMediaPause = 47;
    public static readonly IntPtr HwndBroadcast = new(0xFFFF);

    public const int SmRemotesession = 0x1000;
    public const int MonitorDefaultToNearest = 2;

    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModNorepeat = 0x4000;
    public const int WmHotkey = 0x0312;
    public const int VkL = 0x4C;

    [StructLayout(LayoutKind.Sequential)]
    public struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public uint Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public uint Th32ParentProcessId;
        public int PcPriClassBase;
        public uint DwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }

    public const int WmSyscommand = 0x0112;
    public const int ScMonitorpower = 0xF170;
    /// <summary>SC_MONITORPOWER lParam：2=关闭显示器。</summary>
    public static readonly IntPtr MonitorPowerOff = new(2);

    [DllImport("user32.dll")]
    public static extern bool LockWorkStation();

    [DllImport("user32.dll")]
    public static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    public static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new System.Text.StringBuilder(256);
        var n = GetClassName(hWnd, sb, sb.Capacity);
        return n > 0 ? sb.ToString() : string.Empty;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW")]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Process32NextW")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>取得指定进程的所有后代 PID；停止应用前调用，避免主进程退出后子进程脱离而遗漏。</summary>
    public static HashSet<int> GetDescendantProcessIds(IEnumerable<int> rootProcessIds)
    {
        var result = rootProcessIds.Where(id => id > 0).ToHashSet();
        if (result.Count == 0) return result;

        var snapshot = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snapshot == IntPtr.Zero || snapshot == InvalidHandleValue)
            return result;

        try
        {
            var entries = new List<(int Id, int ParentId)>();
            var entry = new ProcessEntry32 { DwSize = (uint)Marshal.SizeOf<ProcessEntry32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    entries.Add(((int)entry.Th32ProcessId, (int)entry.Th32ParentProcessId));
                    entry.DwSize = (uint)Marshal.SizeOf<ProcessEntry32>();
                } while (Process32Next(snapshot, ref entry));
            }

            bool changed;
            do
            {
                changed = false;
                foreach (var (id, parentId) in entries)
                {
                    if (result.Contains(parentId) && result.Add(id))
                        changed = true;
                }
            } while (changed);

            return result;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    /// <summary>成功返回空闲时长；失败返回 false（调用方不得当作「刚有输入」）。</summary>
    public static bool TryGetIdleTime(out TimeSpan idle)
    {
        idle = TimeSpan.Zero;
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
            return false;

        uint idleMs = unchecked((uint)Environment.TickCount - info.DwTime);
        idle = TimeSpan.FromMilliseconds(idleMs);
        return true;
    }

    /// <summary>兼容旧调用：失败时返回极大空闲，避免被当成「有人在操作」。</summary>
    public static TimeSpan GetIdleTimeSafe()
        => TryGetIdleTime(out var idle) ? idle : TimeSpan.FromDays(1);

    public const byte VkMediaPlayPause = 0xB3;
    private const uint KeyeventfKeyup = 0x0002;
    private const uint InputKeyboard = 1;

    public static void MediaStop() => SendAppCommand(AppcommandMediaStop);
    public static void MediaPause() => SendAppCommand(AppcommandMediaPause);
    public static void MediaPlay() => SendAppCommand(AppcommandMediaPlay);
    public static void MediaPlayPause() => SendAppCommand(AppcommandMediaPlayPause);

    /// <summary>模拟硬件多媒体「播放/暂停」键（比 WM_APPCOMMAND 广播更接近真键盘）。</summary>
    public static void SendMediaPlayPauseKey()
    {
        var inputs = new Input[]
        {
            KeyInput(VkMediaPlayPause, keyUp: false),
            KeyInput(VkMediaPlayPause, keyUp: true)
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static void SendAppCommand(int appCommand)
    {
        var lParam = (IntPtr)(appCommand << 16);
        // 必须用 PostMessage：SendMessage 广播会同步等待所有顶层窗口处理，
        // 任一窗口卡住（或处理慢）就会把调用线程永久冻住，表现为「未响应」。
        PostMessage(HwndBroadcast, WmAppcommand, IntPtr.Zero, lParam);
    }

    private static Input KeyInput(byte vk, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Ki = new KeybdInput
            {
                WVk = vk,
                WScan = 0,
                DwFlags = keyUp ? KeyeventfKeyup : 0,
                Time = 0,
                DwExtraInfo = IntPtr.Zero
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mi;
        [FieldOffset(0)] public KeybdInput Ki;
        [FieldOffset(0)] public HardwareInput Hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeybdInput
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HardwareInput
    {
        public uint UMsg;
        public ushort WParamL;
        public ushort WParamH;
    }

    public static bool IsRemoteSession() => GetSystemMetrics(SmRemotesession) != 0;

    /// <summary>前台窗口是否真全屏（贴满显示器且超出工作区，排除普通最大化）。</summary>
    public static bool IsForegroundFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == GetShellWindow())
            return false;

        if (!GetWindowRect(hwnd, out var wnd))
            return false;

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var mi = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref mi))
            return false;

        const int tolerance = 8;
        var coversMonitor = wnd.Left <= mi.Monitor.Left + tolerance
                            && wnd.Top <= mi.Monitor.Top + tolerance
                            && wnd.Right >= mi.Monitor.Right - tolerance
                            && wnd.Bottom >= mi.Monitor.Bottom - tolerance;
        if (!coversMonitor) return false;

        // 普通最大化通常贴齐工作区；真全屏会盖住任务栏区域。
        var coversOnlyWork = wnd.Left >= mi.Work.Left - tolerance
                             && wnd.Top >= mi.Work.Top - tolerance
                             && wnd.Right <= mi.Work.Right + tolerance
                             && wnd.Bottom <= mi.Work.Bottom + tolerance;
        return !coversOnlyWork;
    }
}
