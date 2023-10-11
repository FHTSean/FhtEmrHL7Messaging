using System.Diagnostics;
using System.ServiceProcess;

namespace FHTMessageService;

public class Program
{
    public static void Main(string[] args)
    {
        // Setup service
        bool isService = !(Debugger.IsAttached || args.Contains("--console"));
        if (isService)
        {
            string pathToExe = Environment.ProcessPath;
            string pathToContentRoot = Path.GetDirectoryName(pathToExe);
            Directory.SetCurrentDirectory(pathToContentRoot);
        }

        // Run service
        MessageService messageService = new();
        if (OperatingSystem.IsWindows() && isService)
        {
            ServiceBase.Run(messageService);
        }
        else
        {
            messageService.StartMessageService();
            Console.ReadLine();
            messageService.StopMessageService();
        }
    }
}
