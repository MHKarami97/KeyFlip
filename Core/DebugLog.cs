using System.Diagnostics;

namespace KeyFlip.Core;

public static class DebugLog
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeyFlip", "debug.log");

    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }
}
