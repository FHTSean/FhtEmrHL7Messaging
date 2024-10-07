using FHTMessageService.Logging;

using Microsoft.AspNetCore.Hosting.WindowsServices;

using NetFwTypeLib;

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

        // Add firewall rule
        IConfigurationRoot localConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        CreateFHTFirewallRule(localConfig.GetValue<int>("Port"));

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

    public static void CreateFHTFirewallRule(params int[] ports)
    {
        try
        {
            // Only works on Windows
            if (!OperatingSystem.IsWindows())
                return;

            INetFwPolicy2 firewallPolicy = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
            // Remove old policies
            Log.WriteLine("Removing old FHT Message Service firewall rules");
            firewallPolicy.Rules.Remove("FHT Message Service");

            // Create new policies
            Log.WriteLine("Creating Windows firewall rule for FHT Message Service");
            // Get firewall policy information
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            INetFwPolicy2 fwPolicy2 = (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
            int currentProfiles = fwPolicy2.CurrentProfileTypes;
            // Create new inbound rule
            INetFwRule2 inboundRule = (INetFwRule2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FWRule"));
            inboundRule.Enabled = true;
            // Allow through firewall
            inboundRule.Action = NET_FW_ACTION_.NET_FW_ACTION_ALLOW;
            // Using protocol TCP
            inboundRule.Protocol = (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            string localPorts = string.Join(",", ports);
            inboundRule.LocalPorts = localPorts;
            Log.Write($"Using ports: ", LogFormat.Bold);
            Log.WriteLine(localPorts);
            // Name of rule
            inboundRule.Name = "FHT Message Service";
            inboundRule.Profiles = currentProfiles;

            // Add FHT rule to existing policies
            firewallPolicy.Rules.Add(inboundRule);

            Log.WriteLine("Firewall rule created", LogFormat.BrightGreen);
        }
        catch (Exception e)
        {
            Log.WriteErrorLine(e);
        }
    }
}
