namespace KeyceCode.Services;

/// <summary>Point unique de journalisation des exceptions non interceptées.</summary>
public sealed class GlobalExceptionHandler : IDisposable
{
    private readonly IApplicationLogger logger;
    private readonly Action<Exception>? notify;
    private bool disposed;

    private GlobalExceptionHandler(IApplicationLogger logger, Action<Exception>? notify)
    {
        this.logger = logger;
        this.notify = notify;
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static GlobalExceptionHandler Install(IApplicationLogger logger, Action<Exception>? notify = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        return new GlobalExceptionHandler(logger, notify);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Application.ThreadException -= OnThreadException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    private void OnThreadException(object? sender, System.Threading.ThreadExceptionEventArgs args) => Handle(args.Exception, "ui-thread");

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception) Handle(exception, "app-domain");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        logger.Error(args.Exception, "unobserved-task");
        args.SetObserved();
    }

    private void Handle(Exception exception, string operation)
    {
        logger.Error(exception, operation);
        try { notify?.Invoke(exception); }
        catch (Exception notificationError) { logger.Error(notificationError, "exception-notification"); }
    }
}
