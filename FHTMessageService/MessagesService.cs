using System.ServiceProcess;

namespace FHTMessageService;

public class MessagesService(WebApplication app) : ServiceBase
{
    protected override void OnStart(string[] args)
    {
        base.OnStart(args);
        app.RunAsync();
    }

    protected override void OnStop()
    {
        base.OnStop();
        app.StopAsync();
    }
}
