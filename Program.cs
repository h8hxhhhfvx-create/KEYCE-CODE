namespace KeyceCode;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        // Les ViewModels WPF sont créés pendant l'initialisation de Form1.
        // Installer explicitement le contexte WinForms garantit que les sorties
        // des processus reviennent bien sur le thread UI lors de l'hébergement WPF.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new Form1());
    }    
}
