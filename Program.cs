using KeyceCode.Services;

namespace KeyceCode;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static int Main(string[]? args = null)
    {
        if (args is { Length: >= 2 } && string.Equals(args[0], "--keyce-extension-host", StringComparison.OrdinalIgnoreCase))
            return ExtensionHostRunner.Run(args[1]);
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        // Les ViewModels WPF sont créés pendant l'initialisation de Form1.
        // Installer explicitement le contexte WinForms garantit que les sorties
        // des processus reviennent bien sur le thread UI lors de l'hébergement WPF.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var logger = new ApplicationLogger();
        using var exceptionHandler = GlobalExceptionHandler.Install(logger, exception =>
            MessageBox.Show($"Une erreur inattendue est survenue.\n{exception.Message}", "KEYCE CODE", MessageBoxButtons.OK, MessageBoxIcon.Error));
        logger.Info("Application démarrée", "startup");
        Application.Run(new Form1());
        logger.Info("Application fermée", "shutdown");
        return 0;
    }    
}
