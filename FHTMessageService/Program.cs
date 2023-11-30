using FHTMessageService.Logging;

using Microsoft.AspNetCore.Hosting.WindowsServices;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.ServiceProcess;
using System.Text;

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

    [RequiresUnreferencedCode("Controllers may be trimmed")]
    public static void Main(string[] args)
    {
        // Setup console
        Console.SetError(new ConsoleWriter(Console.Error, ConsoleColor.Red));
        if (args.Contains("--no-format"))
        {
            Log.UseFormat = false;
        }

        // Setup service
        bool isService = !(Debugger.IsAttached || args.Contains("--console"));
        if (isService)
        {
            string pathToExe = Environment.ProcessPath;
            string pathToContentRoot = Path.GetDirectoryName(pathToExe);
            Directory.SetCurrentDirectory(pathToContentRoot);
        }

        // Create web host
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        WebApplication app = builder.Build();
        app.UseWebSockets();
        app.MapControllers();
        if (isService)
        {
            ServiceBase.Run(new MessagesService(app));
        }
        else
        {
            app.Run();
        }
    }
}
