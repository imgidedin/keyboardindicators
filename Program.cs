namespace KeyboardIndicators;

static class Program
{
    [STAThread]
    static void Main()
    {
        RegisterGlobalExceptionHandlers();

        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            LogUnhandledException("Main", ex);
            throw;
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        Application.ThreadException += (_, e) => LogUnhandledException("Application.ThreadException", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = e.ExceptionObject as Exception
                ?? new Exception($"Unhandled non-Exception object: {e.ExceptionObject}");
            LogUnhandledException("AppDomain.CurrentDomain.UnhandledException", exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogUnhandledException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogUnhandledException(string source, Exception ex)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "KeyboardIndicators.log");
            var lines = new[]
            {
                "==================================================",
                $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}",
                $"Source: {source}",
                $"OS: {Environment.OSVersion}",
                $".NET: {Environment.Version}",
                $"Executable: {Application.ExecutablePath}",
                $"Message: {ex.Message}",
                "Exception:",
                ex.ToString(),
                string.Empty
            };

            File.AppendAllLines(logPath, lines);
        }
        catch
        {
            // Logging must never crash the app harder than the original failure.
        }
    }
}
