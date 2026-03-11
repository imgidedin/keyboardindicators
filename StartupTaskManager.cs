using System.Runtime.Versioning;
using Windows.ApplicationModel;

namespace KeyboardIndicators;

[SupportedOSPlatform("windows10.0.17763.0")]
internal static class StartupTaskManager
{
    private const string StartupTaskId = "KeyboardIndicatorsStartup";

    public static async Task<StartupTaskState?> GetStateAsync()
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            return startupTask.State;
        }
        catch
        {
            return null;
        }
    }

    public static async Task<StartupTaskState?> SetEnabledAsync(bool enabled)
    {
        try
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                return await startupTask.RequestEnableAsync();
            }

            startupTask.Disable();
            return startupTask.State;
        }
        catch
        {
            return null;
        }
    }
}
