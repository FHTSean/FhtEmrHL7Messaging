using System.ServiceProcess;

using FHTMessageService.Client;
using FHTMessageService.Messages;
using FHTMessageService.Models;

using FhtSharedLibrary.SharedFunctions;
using FhtSharedLibrary.ViewModels;

using HL7.Dotnetcore;

using Microsoft.Extensions.Configuration;

using Bps = FhtSharedLibrary.EntityModels.BestPracticeJadeSP2.BPSPatients;
using MdPath = FhtSharedLibrary.EntityModels.MD3;

namespace FHTMessageService;

/// <summary>
/// <see cref="ServiceBase"/> for the FHT message service.
/// Used for installing the application as a service.
/// </summary>
public class MessageService : ServiceBase
{
    private static bool applicationIsRunning = true;
    private static bool applicationIsPaused = false;

    private IConfigurationRoot localConfig;
    private MessageServiceConfigModel remoteConfig;

    /// <summary>
    /// Start the message service manually.
    /// Used when not running as a service, i.e. from command line.
    /// </summary>
    public void StartMessageService()
    {
        OnStart(Array.Empty<string>());
    }

    /// <summary>
    /// Stop the message service manually.
    /// Used when not running as a service, i.e. from command line.
    /// </summary>
    public void StopMessageService()
    {
        OnStop();
    }

    /// <inheritdoc/>
    protected override void OnStart(string[] args)
    {
        applicationIsRunning = true;
        new Thread(async () => await StartService())
        {
            Name = "FHTMessageService",
            IsBackground = true
        }.Start();
    }

    /// <inheritdoc/>
    protected override void OnStop()
    {
        applicationIsRunning = false;
    }

    /// <inheritdoc/>
    protected override void OnPause()
    {
        applicationIsPaused = true;
    }

    /// <inheritdoc/>
    protected override void OnContinue()
    {
        applicationIsPaused = true;
    }

    /// <summary>
    /// Run the message service as a loop, while the application is running.
    /// </summary>
    private async Task StartService()
    {
        Console.WriteLine("Starting message service");
        // Parse local config
        localConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        Console.WriteLine("Local config obtained");
        Console.WriteLine();

        int delay = localConfig.GetValue<int>("DelayMilliseconds");
        while (applicationIsRunning)
        {
            try
            {
                if (!applicationIsPaused)
                {
                    await RunService();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Thread.Sleep(delay);

                Console.WriteLine();
                Console.WriteLine("---");
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Run an iteration of the message service.
    /// Checks for messages and saves them as .hl7 files.
    /// </summary>
    private async Task RunService()
    {
        // Create remote api client
        string awsUrl = localConfig.GetValue<string>("AwsUrl");
        Console.WriteLine($"Using remote API endpoint: {awsUrl}");
        ApiClient remoteApiClient = new(awsUrl);

        // Get client config path
        string path = "clientconfig.txt";
        string applicationDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        Console.WriteLine($"Application dir: {applicationDir}");
        string parentDir = Path.GetDirectoryName(applicationDir);
        Console.WriteLine($"Parent dir: {parentDir}");
        string clientConfigPath = Path.Combine(parentDir, path);
        Console.WriteLine($"Client config file path: {clientConfigPath}");
        // Parse client config
        CryptoFunctions cryptoDecrypt = new();
        using StreamReader clientConfigReader = File.OpenText(clientConfigPath);
        string[] clientConfigLines = clientConfigReader.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] user = clientConfigLines[1].Split(new string[] { "User=" }, StringSplitOptions.None);
        string userName = user[1];
        string[] pwd = clientConfigLines[2].Split(new string[] { "Password=" }, StringSplitOptions.None);
        string password = cryptoDecrypt.Decrypt(pwd[1]);
        Console.WriteLine($"Username: '{userName}', Password: '{password}'");
        Console.WriteLine();

        // Parse remote config
        LoginInfo remoteLoginInfo = new(userName, password);
        UserInfo remoteUserInfo = await remoteApiClient.PostJson<UserInfo>("login", remoteLoginInfo);
        if (remoteUserInfo != null)
        {
            Console.WriteLine($"Login successful: {remoteUserInfo.UserName} (Account ID: {remoteUserInfo.AccountId})");
            // Set client token
            remoteApiClient.SetToken(remoteUserInfo.Token);

            int softwareId = localConfig.GetValue<int>("SoftwareId");
            // Get config info
            remoteConfig = await remoteApiClient.GetConfigInfo(remoteUserInfo.AccountId, softwareId);
            if (remoteConfig != null)
            {
                Console.WriteLine("Remote config obtained");
                // Apply config connection strings
                System.Configuration.ConfigurationManager.AppSettings["BPINSTANCE_ConnectionString"] = remoteConfig.BpDatabaseConnectionString;
                System.Configuration.ConfigurationManager.AppSettings["MDINSTANCEHCN_ConnectionString"] = remoteConfig.MdDatabaseHcnConnectionString;

                // Create local api client
                Console.WriteLine($"Using local API endpoint: {remoteConfig.FhtWebApiEndpoint}");
                ApiClient localApiClient = new(remoteConfig.FhtWebApiEndpoint);

                // Get message from local api
                ResultMessageModel[] messageModels = await localApiClient.GetJson<ResultMessageModel[]>("GetUnsentMessages");

                // Get message path
                string messageDir = GetMessageDir();
                Console.WriteLine($"Message dir: {messageDir}");
                Console.WriteLine();

                // Iterate through each message
                if (messageModels != null && messageModels.Length > 0)
                {
                    foreach (ResultMessageModel messageModel in messageModels)
                    {
                        try
                        {
                            // Write message to emr
                            Message message = HL7MessageUtil.CreateHL7Message(messageModel);
                            string messageFilename = HL7MessageUtil.CreateFilenameFromMessage(messageModel);
                            string messagePath = Path.Join(messageDir, messageFilename);
                            HL7MessageUtil.WriteHL7Message(message, messagePath);
                            Console.WriteLine($"Wrote message: {messagePath}");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No messages to write");
                }
            }
            else
            {
                Console.WriteLine("Error obtaining remote config");
            }
        }
        else
        {
            Console.WriteLine("Remote login error");
        }
    }

    /// <summary>
    /// Get the message directory from the EMR database.
    /// </summary>
    private string GetMessageDir()
    {
        // Check override for message dir
        if (remoteConfig.MessageOutputDir != null)
        {
            return remoteConfig.MessageOutputDir;
        }

        if (remoteConfig.EmrSoftware == "BestPractice")
        {
            // Get import path for BP
            using Bps.BPSPatientsContext fhtBpsContext = new();
            Bps.Reportpaths[] reportPaths = fhtBpsContext.Reportpaths
                .Where(x => x.Recordstatus == 1)
                .ToArray();
            if (reportPaths.Length > 0)
            {
                return reportPaths.First().Reportpath;
            }

            return null;
        }
        else if (remoteConfig.EmrSoftware == "MedicalDirector")
        {
            // Get import path for MD
            using MdPath.HCN.HCNContext fhtMdContext = new();
            MdPath.HCN.MdUpdownConfig[] reportPaths = fhtMdContext.MdUpdownConfig
                .Where(x => x.Enabled == "Y" && x.SdiEnabled == "Y" && x.StampActionCode != "D")
                .ToArray();
            if (reportPaths.Length > 0)
            {
                return reportPaths.First().ImportDirectory;
            }

            return null;
        }

        Console.WriteLine($"Invalid EMR software '{remoteConfig.EmrSoftware}'");
        return null;
    }
}
