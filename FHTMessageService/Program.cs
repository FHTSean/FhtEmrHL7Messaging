using System.Diagnostics;
using System.ServiceProcess;
using System.Text;

using Microsoft.Extensions.Logging;

namespace FHTMessageService;

public class Program
{
    private class ConsoleWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.Default;

        private readonly TextWriter originalStream;
        private readonly ConsoleColor color;

        public ConsoleWriter(TextWriter consoleTextWriter, ConsoleColor color)
        {
            originalStream = consoleTextWriter;
            this.color = color;
        }

        public override void Write(char value)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            originalStream.Write(value);
            Console.ForegroundColor = originalColor;
        }

        public override void Write(string value)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            originalStream.Write(value);
            Console.ForegroundColor = originalColor;
        }
    }

    public static void Main(string[] args)
    {
        // Setup console
        Console.SetError(new ConsoleWriter(Console.Error, ConsoleColor.Red));

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
