using KeyFlip.Core;
using KeyFlip.UI;

namespace KeyFlip;

internal static class Program
{
    internal const string ActivateEventName = "KeyFlip.Activate";

    [STAThread]
    private static void Main(string[] args)
    {
        var isStartupLaunch = args.Any(arg => string.Equals(arg, StartupManager.StartupArg, StringComparison.OrdinalIgnoreCase));

        using var mutex = new Mutex(true, "KeyFlip.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            if (!isStartupLaunch && EventWaitHandle.TryOpenExisting(ActivateEventName, out var activateEvent))
            {
                using (activateEvent)
                {
                    activateEvent.Set();
                }
            }

            return;
        }

        ApplicationConfiguration.Initialize();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new TrayApplicationContext(isStartupLaunch));
    }
}
