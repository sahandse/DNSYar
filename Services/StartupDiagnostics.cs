using System.Runtime.InteropServices;
using System.Text;

namespace DNSYar.Services;

internal static class StartupDiagnostics
{
    private const uint ErrorIcon = 0x00000010;

    public static string Report(Exception exception)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DNSYar");
        var logPath = Path.Combine(directory, "launch-error.txt");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                logPath,
                $"[{DateTimeOffset.Now:O}]{Environment.NewLine}{exception}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch
        {
            // The native dialog below still reports the original startup failure.
        }

        try
        {
            MessageBox(
                IntPtr.Zero,
                $"DNSYar could not start.\n\n{exception.Message}\n\nLog: {logPath}",
                "DNSYar — Startup Error",
                ErrorIcon);
        }
        catch
        {
            // Never replace the original exception with a diagnostics failure.
        }

        return logPath;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr window, string text, string caption, uint type);
}
