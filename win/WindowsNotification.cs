using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Toolkit.Uwp.Notifications;
using SmsLink.Protocol;
using Windows.UI.Notifications;

namespace SmsLink;

internal sealed record NotificationActivation(string? Code);

/// <summary>未打包 WinForms 程序的 Windows Toast 注册、发送与点击激活。</summary>
internal static class WindowsNotification
{
    public const string AppUserModelId = "SmsLink.Desktop";
    private const string ToastActivatorClsid = "6D2665F0-71E8-4D97-AD84-BC171E17366B";
    private const string ToastActivatedArgument = "-ToastActivated";
    private const int ShcneAssocChanged = 0x08000000;

    private static readonly object HandlerLock = new();
    private static readonly ConcurrentQueue<NotificationActivation> PendingActivations = new();
    private static readonly AutoResetEvent ActivationReady = new(false);
    private static Action<NotificationActivation>? _activationHandler;
    private static bool _initialized;

    public static void SetCurrentProcessAppId()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch
        {
            // 旧系统或受限环境下让 Toast 自行失败并回退气泡。
        }
    }

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        SetCurrentProcessAppId();

        try
        {
#pragma warning disable CS0618 // 需要显式 AUMID，使用 Toolkit 为未打包 Win32 提供的兼容入口。
            DesktopNotificationManagerCompat.RegisterAumidAndComServer<ToastActivator>(AppUserModelId);
            DesktopNotificationManagerCompat.RegisterActivator<ToastActivator>();
#pragma warning restore CS0618
        }
        catch
        {
            // 注册失败时发送会回退到托盘气泡。
        }

        try
        {
            EnsureStartMenuShortcut();
        }
        catch
        {
            // 快捷方式创建失败不影响主程序启动。
        }
    }

    public static bool WasToastActivated(string[] args) =>
        args.Any(item => string.Equals(item, ToastActivatedArgument, StringComparison.OrdinalIgnoreCase));

    public static bool Show(SmsMessage message, Action fallback)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240)) return false;
        try
        {
            var source = string.IsNullOrWhiteSpace(message.Source) ? "手机" : message.Source;
            var sender = string.IsNullOrWhiteSpace(message.From) ? "未知发件人" : message.From;
            var body = string.IsNullOrEmpty(message.Code)
                ? message.Body
                : $"验证码 {message.Code}\n{message.Body}";

            var builder = new ToastContentBuilder()
                .AddArgument("action", "open")
                .AddText($"{source} · {sender}")
                .AddText(body);
            if (!string.IsNullOrEmpty(message.Code)) builder.AddArgument("code", message.Code);

            var toast = new ToastNotification(builder.GetToastContent().GetXml());
            toast.Failed += (_, _) => fallback();
#pragma warning disable CS0618 // 与上面的显式 AUMID 注册配套使用。
            DesktopNotificationManagerCompat.CreateToastNotifier().Show(toast);
#pragma warning restore CS0618
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SetActivationHandler(Action<NotificationActivation> handler)
    {
        List<NotificationActivation> pending = [];
        lock (HandlerLock)
        {
            _activationHandler = handler;
            while (PendingActivations.TryDequeue(out var activation)) pending.Add(activation);
        }

        foreach (var activation in pending) handler(activation);
    }

    public static NotificationActivation? WaitForStartupActivation(int millisecondsTimeout)
    {
        if (PendingActivations.TryDequeue(out var activation)) return activation;
        if (!ActivationReady.WaitOne(millisecondsTimeout)) return null;
        return PendingActivations.TryDequeue(out activation) ? activation : null;
    }

    internal static void HandleActivation(string arguments)
    {
        var activation = new NotificationActivation(ReadArgument(arguments, "code"));
        Action<NotificationActivation>? handler;
        lock (HandlerLock)
        {
            handler = _activationHandler;
            if (handler == null) PendingActivations.Enqueue(activation);
        }

        if (handler != null)
        {
            handler(activation);
        }
        else
        {
            ActivationReady.Set();
        }
    }

    private static string? ReadArgument(string arguments, string name)
    {
        foreach (var part in arguments.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal)) continue;
            if (separator < 0 || separator == part.Length - 1) return null;
            return Uri.UnescapeDataString(part[(separator + 1)..]);
        }
        return null;
    }

    private static void EnsureStartMenuShortcut()
    {
        var executable = Environment.ProcessPath ?? Application.ExecutablePath;
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (string.IsNullOrWhiteSpace(programs)) return;

        Directory.CreateDirectory(programs);
        var shortcutPath = Path.Combine(programs, "码上来.lnk");
        // 先删再建，避免对已有快捷方式以只读方式打开后无法写入 AUMID。
        if (File.Exists(shortcutPath)) File.Delete(shortcutPath);

        var shellLinkObject = Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!)!;
        var shellLink = (IShellLinkW)shellLinkObject;
        try
        {
            shellLink.SetPath(executable);
            shellLink.SetWorkingDirectory(Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory);
            shellLink.SetDescription("码上来 - 手机短信同步到电脑");
            shellLink.SetIconLocation(executable, 0);

            var propertyStore = (IPropertyStore)shellLink;
            var appIdKey = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
            var appId = new PropVariant { ValueType = 31, PointerValue = Marshal.StringToCoTaskMemUni(AppUserModelId) };
            try
            {
                ThrowIfFailed(propertyStore.SetValue(ref appIdKey, ref appId));
            }
            finally
            {
                PropVariantClear(ref appId);
            }

            var activatorKey = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 26);
            var guidPointer = Marshal.AllocCoTaskMem(16);
            Marshal.Copy(new Guid(ToastActivatorClsid).ToByteArray(), 0, guidPointer, 16);
            var activator = new PropVariant { ValueType = 72, PointerValue = guidPointer };
            try
            {
                ThrowIfFailed(propertyStore.SetValue(ref activatorKey, ref activator));
            }
            finally
            {
                PropVariantClear(ref activator);
            }

            ThrowIfFailed(propertyStore.Commit());
            ((IPersistFile)shellLink).Save(shortcutPath, true);

            // 让资源管理器重新扫描开始菜单，否则新 AUMID 可能还进不了通知中心。
            SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLinkObject);
        }
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult < 0) Marshal.ThrowExceptionForHR(hresult);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant variant);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort ValueType;
        [FieldOffset(8)] public IntPtr PointerValue;
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(IntPtr file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr itemIdList);
        void SetIDList(IntPtr itemIdList);
        void GetDescription(IntPtr name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory(IntPtr directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments(IntPtr arguments, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation(IntPtr iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint propertyCount);
        void GetAt(uint propertyIndex, out PropertyKey key);
        void GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
}

#pragma warning disable CS0618 // 显式 AUMID 的未打包应用需要 Toolkit 兼容激活器。
[ClassInterface(ClassInterfaceType.None)]
[ComSourceInterfaces(typeof(NotificationActivator.INotificationActivationCallback))]
[Guid("6D2665F0-71E8-4D97-AD84-BC171E17366B")]
[ComVisible(true)]
public sealed class ToastActivator : NotificationActivator
{
    public override void OnActivated(string arguments, NotificationUserInput userInput, string appUserModelId) =>
        WindowsNotification.HandleActivation(arguments);
}
#pragma warning restore CS0618
