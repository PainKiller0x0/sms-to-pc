using SmsLink.Protocol;
using System.Drawing.Imaging;
using System.IO.Pipes;

namespace SmsLink;

internal static class Program
{
    private const string MutexName = @"Local\SmsLink-SingleInstance";
    private const string ShowEventName = @"Local\SmsLink-ShowWindow";
    private const string ActivationPipeName = "SmsLink-ToastActivation";

    /// <summary>
    /// 支持三个调试参数（正常双击运行时用不到）：
    ///   --data &lt;目录&gt;        指定数据目录，便于用干净配置试界面
    ///   --screenshot &lt;文件&gt;   把窗口离屏渲染成 PNG 后退出
    ///   --notify-test           发送一条不含真实数据的测试 Toast 后退出
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        var screenshotPath = ReadOption(args, "--screenshot");
        var notifyTest = args.Contains("--notify-test");
        var debugMode = screenshotPath != null || notifyTest;
        var dataDirectory = ReadOption(args, "--data") ?? (notifyTest
            ? Path.Combine(Path.GetTempPath(), "SmsLink-notify-test", Environment.ProcessId.ToString())
            : Path.Combine(AppContext.BaseDirectory, "data"));

        WindowsNotification.SetCurrentProcessAppId();
        if (screenshotPath == null) WindowsNotification.Initialize();

        var isFirstInstance = true;
        using var mutex = debugMode ? null : new Mutex(initiallyOwned: true, MutexName, out isFirstInstance);

        // 调试截图/通知时允许和正在运行的实例共存：不抢单实例锁、不占端口。
        if (!isFirstInstance && !debugMode)
        {
            if (WindowsNotification.WasToastActivated(args))
            {
                var activation = WindowsNotification.WaitForStartupActivation(5000);
                if (activation != null && SendActivationToFirstInstance(activation)) return;
            }

            // 普通重复启动，或激活参数传递失败：至少叫已有实例显示窗口。
            try
            {
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch
            {
                // 实例刚退出，忽略
            }
            return;
        }

        ApplicationConfiguration.Initialize();
        var config = AppConfig.Load(dataDirectory);

        // 开机自启的勾选状态以注册表为准，避免用户手动删掉注册表后界面还显示已开。
        var registered = MainForm.AutostartRegistered();
        if (!debugMode && registered != config.StartWithWindows)
        {
            config.StartWithWindows = registered;
            config.Save();
        }

        using var showEvent = debugMode
            ? null
            : new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ShowEventName);

        using var form = new MainForm(
            config, config.LoadMessages(), startServer: !debugMode, applyAutostart: !debugMode);
        WindowsNotification.SetActivationHandler(activation =>
        {
            try
            {
                form.BeginInvoke(() => form.HandleNotificationActivation(activation.Code));
            }
            catch
            {
                // 主窗口正在退出时忽略迟到的点击。
            }
        });

        if (notifyTest)
        {
            ConfigureNotifyTest(form);
        }
        else if (screenshotPath != null)
        {
            ConfigureScreenshot(form, screenshotPath, args.Contains("--about"));
        }

        if (!debugMode) StartInstanceWatchers(form, showEvent!);
        Application.Run(form);
    }

    private static void ConfigureNotifyTest(MainForm form)
    {
        form.Shown += (_, _) =>
        {
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            form.ShowNotification(new SmsMessage
            {
                Id = "notify-test",
                Source = "测试设备",
                From = "10086",
                Body = "【测试】这是一条码上来通知测试，验证码 123456",
                Code = "123456",
                Timestamp = now,
                ReceivedAt = now,
            });

            var timer = new System.Windows.Forms.Timer { Interval = 6000 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                form.ExitForDebug();
            };
            timer.Start();
        };
    }

    private static void ConfigureScreenshot(MainForm form, string screenshotPath, bool aboutPage)
    {
        form.Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 700 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                Application.DoEvents();

                if (aboutPage)
                {
                    using var about = new Ui.AboutForm
                    {
                        StartPosition = FormStartPosition.Manual,
                        Location = new Point(form.Left, form.Top),
                    };
                    about.Show();
                    Application.DoEvents();
                    Capture(about, screenshotPath);
                    about.Close();
                    form.ExitForDebug();
                    return;
                }

                try
                {
                    // 固定成默认窗口尺寸再截图，避免调试进程的 DPI 感知和真实窗口对不上。
                    form.ForceScale(1f);
                    Capture(form, screenshotPath);
                    Console.WriteLine($"layout: {form.DescribeLayout()}");
                }
                catch (Exception error)
                {
                    Console.WriteLine($"screenshot failed: {error.Message}");
                }
                form.ExitForDebug();
            };
            timer.Start();
        };
    }

    private static void StartInstanceWatchers(MainForm form, EventWaitHandle showEvent)
    {
        new Thread(() =>
        {
            while (true)
            {
                showEvent.WaitOne();
                try
                {
                    form.BeginInvoke(form.ShowFromTray);
                }
                catch
                {
                    return;
                }
            }
        }) { IsBackground = true }.Start();

        new Thread(() => WatchToastActivations(form)) { IsBackground = true }.Start();
    }

    private static bool SendActivationToFirstInstance(NotificationActivation activation)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", ActivationPipeName, PipeDirection.Out, PipeOptions.None);
            client.Connect(3000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(activation.Code ?? "");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WatchToastActivations(MainForm form)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    ActivationPipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte);
                server.WaitForConnection();
                using var reader = new StreamReader(server);
                var code = reader.ReadLine();
                form.BeginInvoke(() => form.HandleNotificationActivation(
                    string.IsNullOrEmpty(code) ? null : code));
            }
            catch
            {
                return;
            }
        }
    }

    private static void Capture(Form target, string path)
    {
        using var bitmap = new Bitmap(target.ClientSize.Width, target.ClientSize.Height);
        target.DrawToBitmap(bitmap, new Rectangle(Point.Empty, target.ClientSize));
        bitmap.Save(path, ImageFormat.Png);
        Console.WriteLine($"screenshot: {path} ({target.ClientSize.Width}x{target.ClientSize.Height})");
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }
}
