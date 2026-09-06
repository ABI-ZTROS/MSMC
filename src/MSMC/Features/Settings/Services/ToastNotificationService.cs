// -----------------------------------------------------------------------------
// ToastNotificationService.cs
//
// 微软官方流程（严格按 DesktopToast 参考实现）:
//   1. SetCurrentProcessExplicitAppUserModelID — P/Invoke shell32.dll
//   2. IShellLink + IPropertyStore + PKEY_AppUserModel_ID → 创建带 AUMID 的 .lnk
//   3. ToastNotificationManager.CreateToastNotifier(AUMID).Show() — 必须带 AUMID 参数
//
// 参考: github.com/emoacht/DesktopToast (广泛验证的 Win32 Desktop Toast 实现)
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Threading;
using Serilog;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace io.NET.ZTR_OS.Features.Settings.Services;

/// <summary>Toast 通知服务接口</summary>
public interface IToastNotificationService
{
    void SetUiDispatcher(Dispatcher dispatcher);
    void Initialize();
    void ShowInfo(string title, string message, Action<string>? onActivated = null);
    void ShowSuccess(string title, string message, Action<string>? onActivated = null);
    void ShowWarning(string title, string message, Action<string>? onActivated = null);
    void ShowError(string title, string message, Action<string>? onActivated = null);
    void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null);
    void ClearAll();
    event Action<string>? OnToastActivated;
}

// ═══════════════════════════════════════════════════════════════
// COM 类型定义 — 直接抄 emoacht/DesktopToast（经过广泛验证）
// ═══════════════════════════════════════════════════════════════

/// <summary>IShellLinkW — 创建 .lnk 的核心接口</summary>
[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    uint GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, ref WIN32_FIND_DATAW pfd, uint fFlags);
    uint GetIDList(out IntPtr ppidl);
    uint SetIDList(IntPtr pidl);
    uint GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    uint SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    uint GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    uint SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    uint GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    uint SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    uint GetHotKey(out ushort pwHotkey);
    uint SetHotKey(ushort wHotKey);
    uint GetShowCmd(out uint piShowCmd);
    uint SetShowCmd(uint iShowCmd);
    uint GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    uint SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    uint SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    uint Resolve(IntPtr hwnd, uint fFlags);
    uint SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

/// <summary>IPropertyStore — 读写 .lnk 属性（包括 PKEY_AppUserModel_ID）</summary>
[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    uint GetCount([Out] out uint cProps);
    uint GetAt([In] uint iProp, out PropertyKey pkey);
    uint GetValue([In] ref PropertyKey key, [Out] PropVariant pv);
    uint SetValue([In] ref PropertyKey key, [In] PropVariant pv);
    uint Commit();
}

/// <summary>IPersistFile — 把 .lnk 保存到磁盘</summary>
[ComImport, Guid("0000010B-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFile
{
    uint GetClassID(out Guid pClassID);
    uint IsDirty();
    uint Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
    uint Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
    uint SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    uint GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
}

[StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode), Serializable]
internal struct WIN32_FIND_DATAW
{
    public uint dwFileAttributes;
    public ComTypes.FILETIME ftCreationTime;
    public ComTypes.FILETIME ftLastAccessTime;
    public ComTypes.FILETIME ftLastWriteTime;
    public uint dwFileSizeHigh;
    public uint dwFileSizeLow;
    public uint dwReserved0;
    public uint dwReserved1;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public int PropertyId;
    public PropertyKey(string formatId, int propertyId) { FormatId = new Guid(formatId); PropertyId = propertyId; }
}

/// <summary>PropVariant — 支持 VT_LPWSTR（字符串）</summary>
[StructLayout(LayoutKind.Explicit)]
internal sealed class PropVariant : IDisposable
{
    [FieldOffset(0)] private ushort valueType;
    [FieldOffset(8)] private IntPtr value;

    public VarEnum ValueType { get => (VarEnum)valueType; set => valueType = (ushort)value; }

    public PropVariant() { }

    public PropVariant(string str)
    {
        ValueType = VarEnum.VT_LPWSTR;
        value = Marshal.StringToCoTaskMemUni(str);
    }

    public string Value
    {
        get
        {
            if (ValueType == VarEnum.VT_LPWSTR)
                return Marshal.PtrToStringUni(value) ?? string.Empty;
            return string.Empty;
        }
    }

    public void Dispose()
    {
        if (value != IntPtr.Zero)
        {
            switch (ValueType)
            {
                case VarEnum.VT_LPWSTR:
                    Marshal.FreeCoTaskMem(value);
                    break;
            }
            value = IntPtr.Zero;
        }
        ValueType = VarEnum.VT_EMPTY;
    }
}

// ═══════════════════════════════════════════════════════════════
// ToastNotificationService — 微软官方流程实现
// ═══════════════════════════════════════════════════════════════

public class ToastNotificationService : IToastNotificationService
{
    public const string AppUserModelId = "io.NET.ZTR_OS";
    public const string DisplayName = "MSMC";

    // PKEY_AppUserModel_ID: {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, PID=5
    private static readonly PropertyKey PKEY_AppUserModel_ID =
        new("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}", 5);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    [DllImport("ole32.dll")]
    private static extern uint CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
        ref Guid riid, out IntPtr ppv);

    // CLSID_ShellLink = 00021401-0000-0000-C000-000000000046
    private static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IID_IShellLinkW = new("000214F9-0000-0000-C000-000000000046");
    private static readonly Guid IID_IPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly Guid IID_IPersistFile = new("0000010B-0000-0000-C000-000000000046");

    private Dispatcher? _uiDispatcher;
    private bool _initialized;

    public event Action<string>? OnToastActivated;

    public void SetUiDispatcher(Dispatcher dispatcher) => _uiDispatcher = dispatcher;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            // Step 1: 进程级 AUMID
            int hr = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
            Log.Information("[TOAST] SetCurrentProcessExplicitAppUserModelID hr=0x{Hr:X8} AUMID={AUMID}", hr, AppUserModelId);

            // Step 2: 创建带 PKEY_AppUserModel_ID 的 Start Menu .lnk（关键！）
            EnsureStartMenuShortcutWithAumid();

            // Step 3: 订阅激活回调
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                Log.Information("[TOAST] 激活回调: {Args}", args.Argument);
                var d = _uiDispatcher ?? Dispatcher.CurrentDispatcher;
                d.BeginInvoke(() => OnToastActivated?.Invoke(args.Argument));
            };

            _initialized = true;
            Log.Information("[TOAST] ✅ Toast 通知服务初始化完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] Initialize 异常");
            throw;
        }
    }

    /// <summary>
    /// 严格按微软官方 COM 流程创建 .lnk:
    ///   CoCreateInstance(IShellLinkW) → SetPath → SetArguments → IPropertyStore.SetValue(PKEY_AppUserModel_ID)
    ///   IPropertyStore.Commit() → IPersistFile.Save()
    /// 
    /// ⚠️ WScript.Shell 的 Shortcut.AppUserModelID 属性根本不存在！
    ///    之前的代码 silently swallowed，导致 .lnk 没有 AUMID → Toast 静默丢弃
    /// </summary>
    private static void EnsureStartMenuShortcutWithAumid()
    {
        try
        {
            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            string lnkPath = Path.Combine(startMenu, $"{DisplayName}.lnk");

            if (File.Exists(lnkPath))
            {
                Log.Debug("[TOAST] .lnk 已存在，跳过: {Path}", lnkPath);
                return;
            }

            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) { Log.Warning("[TOAST] Environment.ProcessPath 为 null"); return; }

            Directory.CreateDirectory(startMenu);

            // COM 流程 — 严格按 emoacht/DesktopToast + 微软 Windows 官方示例
            uint hr = CoCreateInstance(ref CLSID_ShellLink, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */,
                ref IID_IShellLinkW, out IntPtr pShellLink);
            if (hr > 1) { Log.Warning("[TOAST] CoCreateInstance(ShellLink) HRESULT=0x{Hr:X8}", hr); return; }

            try
            {
                var shellLink = (IShellLinkW)Marshal.GetObjectForIUnknown(pShellLink);

                hr = shellLink.SetPath(exePath);
                if (hr > 1) { Log.Warning("[TOAST] SetPath HRESULT=0x{Hr:X8}", hr); return; }

                hr = shellLink.SetArguments("");
                if (hr > 1) { Log.Warning("[TOAST] SetArguments HRESULT=0x{Hr:X8}", hr); return; }

                hr = shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");
                if (hr > 1) { Log.Warning("[TOAST] SetWorkingDirectory HRESULT=0x{Hr:X8}", hr); return; }

                // QueryInterface → IPropertyStore（COM 接口指针查询）
                Marshal.QueryInterface(pShellLink, ref IID_IPropertyStore, out IntPtr pPropStore);
                var propStore = (IPropertyStore)Marshal.GetObjectForIUnknown(pPropStore);
                Marshal.Release(pPropStore);

                // 设置 PKEY_AppUserModel_ID
                using (var pv = new PropVariant(AppUserModelId))
                {
                    hr = propStore.SetValue(ref PKEY_AppUserModel_ID, pv);
                    if (hr > 1) { Log.Warning("[TOAST] SetValue(PKEY_AppUserModel_ID) HRESULT=0x{Hr:X8}", hr); return; }

                    hr = propStore.Commit();
                    if (hr > 1) { Log.Warning("[TOAST] IPropertyStore.Commit() HRESULT=0x{Hr:X8}", hr); return; }
                }

                // QueryInterface → IPersistFile → 保存
                Marshal.QueryInterface(pShellLink, ref IID_IPersistFile, out IntPtr pPersistFile);
                var persistFile = (IPersistFile)Marshal.GetObjectForIUnknown(pPersistFile);
                Marshal.Release(pPersistFile);

                hr = persistFile.Save(lnkPath, true);
                if (hr > 1) { Log.Warning("[TOAST] IPersistFile.Save() HRESULT=0x{Hr:X8}", hr); return; }

                Log.Information("[TOAST] ✅ Start Menu .lnk 创建: {Path} (PKEY_AppUserModel_ID={AUMID})", lnkPath, AppUserModelId);
            }
            finally
            {
                Marshal.Release(pShellLink);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] COM .lnk 创建异常");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 发送通知
    // ═══════════════════════════════════════════════════════════════

    public void ShowInfo(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowSuccess(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowWarning(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowError(string title, string message, Action<string>? onActivated = null)
        => ShowToastCore(title, message);
    public void ShowCustom(string title, string message, string icon = "Info", Action<string>? onActivated = null)
        => ShowToastCore(title, message);

    /// <summary>
    /// 微软官方: ToastNotificationManager.CreateToastNotifier(AUMID).Show() — 必须带 AUMID！
    /// Toolkit 7.x 的 .Show() 用无参数版 → Win32 Desktop 静默丢弃
    /// </summary>
    private void ShowToastCore(string title, string message)
    {
        try
        {
            if (!_initialized) Initialize();

            var toastXml = new ToastContentBuilder()
                .AddText(title).AddText(message)
                .AddButton(new ToastButton().SetContent("打开 MSMC").AddArgument("action", "open"))
                .GetToastContent().GetXml();

            var toast = new ToastNotification(toastXml);

            // ⚠️ 关键: 带 AUMID 参数
            ToastNotificationManager.CreateToastNotifier(AppUserModelId).Show(toast);

            Log.Information("[TOAST] ✅ Toast 已发送 (AUMID={AUMID}, Title={Title})", AppUserModelId, title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] ❌ Toast 发送失败 (AUMID={AUMID})", AppUserModelId);
        }
    }

    public void ClearAll()
    {
        try { ToastNotificationManagerCompat.History.Clear(); }
        catch (Exception ex) { Log.Warning(ex, "[TOAST] 清除通知历史失败"); }
    }
}
