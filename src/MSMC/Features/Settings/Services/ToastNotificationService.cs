// -----------------------------------------------------------------------------
// 文件名: ToastNotificationService.cs
// 命名空间: io.NET.ZTR_OS.Features.Settings.Services
// 功能描述: Windows Toast 通知服务 — 严格遵循微软官方 Win32 Desktop Toast 流程
//
// 微软官网必读:
//   learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid
//   learn.microsoft.com/en-us/windows/win32/shell/quickstart-sending-desktop-toast
//
// ⚠️ 关键（微软强调）:
//   1. Start Menu .lnk **必须**有 System.AppUserModel.ID 属性（COM IShellLink + IPropertyStore）
//   2. CreateToastNotifier **必须传入** AppUserModelID（带参数！不传 = 静默失败）
//   3. 不能只用 SetCurrentProcessExplicitAppUserModelID（只设进程级，不够）
//
//   Toolkit 7.x 的 ToastContentBuilder.Show() 内部用无参数 CreateToastNotifier()
//   → 在 Win32 Desktop 上**必然静默失败**！必须绕过它
// -----------------------------------------------------------------------------
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Serilog;
using Microsoft.Toolkit.Uwp.Notifications;

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
// COM 接口定义 — 按微软 Windows SDK 签名
// 参考: Windows SDK <shobjidl.h> <propsys.h>
// ═══════════════════════════════════════════════════════════════

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    [PreserveSig] int GetPath([Out] char[] pszFile, int cchMaxPath, out IntPtr pfd, int fFlags);
    [PreserveSig] int GetIDList(out IntPtr ppidl);
    [PreserveSig] int SetIDList(IntPtr pidl);
    [PreserveSig] int GetDescription([Out] char[] pszName, int cchMaxName);
    [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    [PreserveSig] int GetWorkingDirectory([Out] char[] pszDir, int cchMaxPath);
    [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    [PreserveSig] int GetArguments([Out] char[] pszArgs, int cchMaxPath);
    [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    [PreserveSig] int GetHotkey(out short pwHotkey);
    [PreserveSig] int SetHotkey(short wHotkey);
    [PreserveSig] int GetShowCmd(out int piShowCmd);
    [PreserveSig] int SetShowCmd(int iShowCmd);
    [PreserveSig] int GetIconLocation([Out] char[] pszIconPath, int cchIconPath, out int piIcon);
    [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszRelPath, int dwReserved);
    [PreserveSig] int Resolve(IntPtr hwnd, int fFlags);
    [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out int cProps);
    [PreserveSig] int GetAt(int iProp, out PROPERTYKEY pkey);
    [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
    [PreserveSig] int Commit();
}

[ComImport]
[Guid("0000010b-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistFile
{
    [PreserveSig] int GetClassID(out Guid pClassID);
    [PreserveSig] int IsDirty();
    [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
    [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
    [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    public Guid FmtID;
    public int PID;
    public PROPERTYKEY(Guid fmtId, int pid) { FmtID = fmtId; PID = pid; }
}

[StructLayout(LayoutKind.Explicit)]
internal struct PROPVARIANT
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public IntPtr pwszVal;
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
class ShellLinkCoClass { }

// P/Invoke: ole32.dll
[DllImport("ole32.dll", CharSet = CharSet.Unicode)]
extern static int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

// ═══════════════════════════════════════════════════════════════
// 实现
// ═══════════════════════════════════════════════════════════════

public class ToastNotificationService : IToastNotificationService
{
    public const string AppUserModelId = "io.NET.ZTR_OS";
    public const string DisplayName = "MSMC";

    // PKEY_AppUserModel_ID: {9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}, PID=5
    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    private Dispatcher? _uiDispatcher;
    private bool _initialized;

    public event Action<string>? OnToastActivated;

    public void SetUiDispatcher(Dispatcher dispatcher) => _uiDispatcher = dispatcher;

    public void Initialize()
    {
        if (_initialized) return;

        try
        {
            // 1) 确保 COM STA 已初始化
            CoInitializeEx(IntPtr.Zero, 0 /* COINIT_APARTMENTTHREADED */);

            // 2) 创建 Start Menu .lnk + 设置 PKEY_AppUserModel_ID
            //    微软要求: 没有带 AUMID 的 .lnk → Toast 静默丢弃
            int hr = EnsureStartMenuShortcutWithAumid();
            if (hr < 0)
                Log.Warning("[TOAST] ⚠️ Start Menu Shortcut 创建失败 HRESULT=0x{Hr:X8} — Toast 可能静默丢弃！", hr);
            else
                Log.Information("[TOAST] ✅ Start Menu Shortcut 就绪 (HRESULT=0x{Hr:X8})", hr);

            // 3) 订阅 Toolkit 的 OnActivated（点击激活回调）
            ToastNotificationManagerCompat.OnActivated += args =>
            {
                Log.Information("[TOAST] 激活回调: {Args}", args.Argument);
                var d = _uiDispatcher ?? Dispatcher.CurrentDispatcher;
                d.BeginInvoke(() => OnToastActivated?.Invoke(args.Argument));
            };

            _initialized = true;
            Log.Information("[TOAST] ✅ Toast 通知服务初始化完成 (AUMID={AUMID})", AppUserModelId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] Initialize 异常");
            throw;
        }
    }

    /// <summary>
    /// 严格按微软官方流程创建 Start Menu .lnk
    /// 参考: learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid
    /// </summary>
    private static int EnsureStartMenuShortcutWithAumid()
    {
        string startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs");
        string lnkPath = Path.Combine(startMenu, $"{DisplayName}.lnk");

        if (File.Exists(lnkPath))
        {
            Log.Debug("[TOAST] .lnk 已存在，跳过创建: {Path}", lnkPath);
            return 1; // S_FALSE
        }

        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            Log.Warning("[TOAST] Environment.ProcessPath 为 null");
            return unchecked((int)0x80070057);
        }

        return CreateLnk(lnkPath, exePath);
    }

    /// <summary>
    /// 创建带 PKEY_AppUserModel_ID 的 .lnk（微软官方流程）
    ///   1. CoCreateInstance(CLSID_ShellLink) → IShellLinkW
    ///   2. shellLink.SetPath / SetArguments / SetWorkingDirectory
    ///   3. QueryInterface → IPropertyStore → SetValue(PKEY_AppUserModel_ID) + Commit
    ///   4. QueryInterface → IPersistFile → Save(lnkPath)
    /// </summary>
    private static int CreateLnk(string lnkPath, string exePath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnkPath)!);

            // 1. 创建 IShellLink
            var shellLink = (IShellLinkW)new ShellLinkCoClass();

            // 2. 设置目标
            int hr = shellLink.SetPath(exePath);
            if (hr < 0) { Log.Warning("[TOAST] SetPath 失败 0x{Hr:X8}", hr); return hr; }
            shellLink.SetArguments("");
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(exePath) ?? "");

            // 3. 设置 PKEY_AppUserModel_ID（关键！）
            var propStore = (IPropertyStore)shellLink;
            using var bstr = new Bstr(AppUserModelId);
            var pv = new PROPVARIANT { vt = 31 /* VT_LPWSTR */, pwszVal = bstr.Ptr };
            hr = propStore.SetValue(ref PKEY_AppUserModel_ID, ref pv);
            if (hr < 0) { Log.Warning("[TOAST] SetValue(AUMID) 失败 0x{Hr:X8}", hr); return hr; }

            hr = propStore.Commit();
            if (hr < 0) { Log.Warning("[TOAST] Commit 失败 0x{Hr:X8}", hr); return hr; }

            // 4. 保存到磁盘
            var persistFile = (IPersistFile)shellLink;
            hr = persistFile.Save(lnkPath, true);
            if (hr < 0) { Log.Warning("[TOAST] .lnk Save 失败 0x{Hr:X8}", hr); return hr; }

            Log.Information("[TOAST] ✅ Start Menu .lnk 创建成功: {Path} (AUMID={AUMID})", lnkPath, AppUserModelId);
            return 0; // S_OK
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] CreateLnk 异常");
            return -1;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 发送通知 — 严格按微软官方流程
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
    /// 微软官方流程:
    ///   1. ToastContentBuilder 生成 XML（简化构造）
    ///   2. new ToastNotification(xml)
    ///   3. ⚠️ **带 AUMID 参数**的 CreateToastNotifier(appUserModelId)
    ///      微软强调: 不传 AUMID → Toast 在 Win32 Desktop 上静默丢弃
    ///      Toolkit 7.x 的 .Show() 用的是无参数版 → 这就是它发不出通知的原因！
    /// </summary>
    private void ShowToastCore(string title, string message)
    {
        try
        {
            if (!_initialized) Initialize();

            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .AddButton(new ToastButton()
                    .SetContent("打开 MSMC")
                    .AddArgument("action", "open"));

            var toastXml = builder.GetToastContent().GetXml();
            var toast = new ToastNotification(toastXml);

            // ⚠️ 关键: 带 AUMID 参数 —— 微软官方明确要求！
            var notifier = ToastNotificationManager.CreateToastNotifier(AppUserModelId);
            notifier.Show(toast);

            Log.Information("[TOAST] ✅ Toast 已发送 (AUMID={AUMID}, Title={Title})", AppUserModelId, title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TOAST] ❌ Toast 发送失败 (AUMID={AUMID})", AppUserModelId);
        }
    }

    public void ClearAll()
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
            Log.Information("[TOAST] ✅ 通知历史已清除");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[TOAST] 清除通知历史失败");
        }
    }

    /// <summary>IDisposable BSTR 包装</summary>
    private readonly struct Bstr : IDisposable
    {
        public IntPtr Ptr { get; }
        public Bstr(string s) => Ptr = Marshal.StringToBSTR(s);
        public void Dispose() => Marshal.FreeBSTR(Ptr);
    }
}
