namespace CopyOCR;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "CopyOCR.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
