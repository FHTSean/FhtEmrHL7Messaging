﻿using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;

using FHTMessageService.Client;
using FHTMessageService.Logging;
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

    private int serviceDelayMilliseconds = 60000;

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
        Log.WriteLine("Starting message service", LogFormat.Bold, LogFormat.Italic);
        // Parse local config
        localConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        Log.WriteLine("Local config obtained", LogFormat.Italic);
        Log.WriteLine();

        serviceDelayMilliseconds = localConfig.GetValue<int>("DelayMilliseconds");
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
                Log.WriteErrorLine(e);
            }
            finally
            {
                Log.WriteLine($"Service complete", LogFormat.Italic);
                Log.WriteLine($"Waiting {serviceDelayMilliseconds}ms", LogFormat.Bold, LogFormat.Italic);
                Thread.Sleep(serviceDelayMilliseconds);

                Log.WriteLine();
                Log.WriteLine("---");
                Log.WriteLine();
            }
        }
    }

    /// <summary>
    /// Run an iteration of the message service.
    /// Checks for messages and saves them as .hl7 files.
    /// </summary>
    private async Task RunService()
    {
        // Create remote API client
        string awsUrl = localConfig.GetValue<string>("AwsUrl");
        Log.Write("Using remote API endpoint: ", LogFormat.Bold);
        Log.WriteLine(awsUrl, LogFormat.BrightCyan);
        ApiClient remoteApiClient = new(awsUrl);

        // Get client config path
        string path = "clientconfig.txt";
        string applicationDir = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
        Log.Write("Application dir: ", LogFormat.Bold);
        Log.WriteLine(applicationDir);
        string parentDir = Path.GetDirectoryName(applicationDir);
        Log.Write("Parent dir: ", LogFormat.Bold);
        Log.WriteLine(parentDir);
        string clientConfigPath = Path.Combine(parentDir, path);
        Log.Write("Client config file path: ", LogFormat.Bold);
        Log.WriteLine(clientConfigPath);
        // Parse client config
        CryptoFunctions cryptoDecrypt = new();
        using StreamReader clientConfigReader = File.OpenText(clientConfigPath);
        string[] clientConfigLines = clientConfigReader.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] configUser = clientConfigLines[1].Split(new string[] { "User=" }, 2, StringSplitOptions.None);
        string username = configUser.Length == 2 ? configUser[1] : null;
        string[] configPassword = clientConfigLines[2].Split(new string[] { "Password=" }, 2, StringSplitOptions.None);
        string password = configPassword.Length == 2 ? cryptoDecrypt.Decrypt(configPassword[1]) : null;
        Log.Write("Username: ", LogFormat.Bold);
        Log.Write(username, LogFormat.BrightMagenta);
        Log.Write(", ");
        Log.Write("Password: ", LogFormat.Bold);
        Log.Write('('); Log.Write(password, LogFormat.Conceal); Log.Write(')');
        Log.WriteLine();
        Log.WriteLine();

        // Parse remote config
        LoginInfo remoteLoginInfo = new(username, password);
        UserInfo remoteUserInfo = await remoteApiClient.PostJson<UserInfo, LoginInfo>("login", remoteLoginInfo);
        if (remoteUserInfo != null)
        {
            Log.WriteLine("Remote login successful", LogFormat.Italic, LogFormat.BrightGreen);
            Log.Write("Username: ", LogFormat.Bold);
            Log.Write(remoteUserInfo.UserName, LogFormat.BrightMagenta);
            Log.Write(", ");
            Log.Write("Account ID: ", LogFormat.Bold);
            Log.Write(remoteUserInfo.AccountId);
            Log.WriteLine();
            // Set client token
            remoteApiClient.SetToken(remoteUserInfo.Token);

            int softwareId = localConfig.GetValue<int>("SoftwareId");
            // Get remote config info
            remoteConfig = await remoteApiClient.GetConfigInfo(remoteUserInfo.AccountId, softwareId);
        }
        else
        {
            Log.WriteErrorLine("Remote login error");
        }

        if (remoteConfig != null)
        {
            // Successfully obtained remote config
            Log.WriteLine("Remote config obtained", LogFormat.Italic);
        }
        else
        {
            // Use empty config if no config exists
            Log.WriteLine("Could not find remote config, using default values", LogFormat.Italic, LogFormat.Yellow);
            remoteConfig = new MessageServiceConfigModel();
        }

        // Override service delay
        if (remoteConfig.ServiceDelayMilliseconds.HasValue)
        {
            serviceDelayMilliseconds = remoteConfig.ServiceDelayMilliseconds.Value;
        }

        // Apply BP config connection string from remote config
        if (remoteConfig.BpDatabaseConnectionString != null)
        {
            System.Configuration.ConfigurationManager.AppSettings["BPINSTANCE_ConnectionString"] = remoteConfig.BpDatabaseConnectionString;
        }
        else
        {
            System.Configuration.ConfigurationManager.AppSettings["BPINSTANCE_ConnectionString"] = localConfig.GetConnectionString("BpDatabaseConnectionString");
        }

        // Apply MD config connection string from remote config
        if (remoteConfig.MdDatabaseHcnConnectionString != null)
        {
            System.Configuration.ConfigurationManager.AppSettings["MDINSTANCEHCN_ConnectionString"] = remoteConfig.MdDatabaseHcnConnectionString;
        }
        else
        {
            System.Configuration.ConfigurationManager.AppSettings["MDINSTANCEHCN_ConnectionString"] = localConfig.GetConnectionString("MdDatabaseHcnConnectionString");
        }

        // Get local API endpoint
        string localApiEndpoint;
        if (remoteConfig.FhtWebApiEndpoint != null)
        {
            localApiEndpoint = remoteConfig.FhtWebApiEndpoint;
            Log.WriteLine("Using local API endpoint from config", LogFormat.Italic);
        }
        else
        {
            localApiEndpoint = GetLocalApiEndpoint();
            Log.WriteLine("Obtained local API endpoint from UDP", LogFormat.Italic);
        }

        Log.Write("Local FHT API endpoint: ", LogFormat.Bold);
        Log.WriteLine(localApiEndpoint, LogFormat.BrightCyan);
        Log.WriteLine();

        // Create local API client
        ApiClient localApiClient = new(localApiEndpoint);

        // Get message from local API
        ResultMessageModel[] messageModels = await localApiClient.GetJson<ResultMessageModel[]>("GetUnsentMessages");

        // Iterate through each message
        if (messageModels != null && messageModels.Length > 0)
        {
            Log.WriteLine($"{messageModels.Length} {(messageModels.Length == 1 ? "message" : "messages")} from API");

            // Get message path
            Dictionary<string, string> messageDirs = messageModels
                .Select(x => x.Patient.PatientEmr).Distinct()
                .ToDictionary(x => x, GetMessageDir);
            if (messageDirs.Any())
            {
                foreach (string emrSoftware in messageDirs.Keys)
                {
                    Log.WriteLine($"Message dir ({emrSoftware}): {messageDirs[emrSoftware]}");
                }
            }
            else
            {
                Log.WriteErrorLine($"Could not find message dir");
            }

            Log.WriteLine();
            foreach (ResultMessageModel messageModel in messageModels)
            {
                try
                {
                    // Write message to emr
                    Message message = HL7MessageUtil.CreateHL7Message(messageModel);
                    string messageFilename = HL7MessageUtil.CreateFilenameFromMessage(messageModel);
                    string messagePath = Path.Join(messageDirs[messageModel.Patient.PatientEmr], messageFilename);
                    HL7MessageUtil.WriteHL7Message(message, messagePath);
                    Log.WriteLine($"Wrote message: {messagePath}", LogFormat.Italic, LogFormat.Green);
                }
                catch (Exception e)
                {
                    Log.WriteErrorLine(e);
                }
            }
        }
        else
        {
            Log.WriteLine("No messages to write", LogFormat.Italic);
        }

        Log.WriteLine();
    }

    /// <summary>
    /// Get the endpoint for the local FHT API using UDP.
    /// </summary>
    /// <returns></returns>
    private string GetLocalApiEndpoint()
    {
        try
        {
            using UdpClient udpClient = new(localConfig.GetValue<int>("MulticastPort"), AddressFamily.InterNetwork);
            udpClient.Client.ReceiveTimeout = 20000;

            IPAddress groupAddress = IPAddress.Parse(localConfig.GetValue<string>("MulticastAddress"));
            // Join group
            udpClient.JoinMulticastGroup(groupAddress);
            IPEndPoint webApiDest = new(groupAddress, localConfig.GetValue<int>("MulticastTargetPort"));
            // Send byte
            udpClient.Send(new byte[] { 1 }, 1, webApiDest);
            // Wait for response
            IPEndPoint endpoint = new(IPAddress.Any, 50);
            byte[] data = udpClient.Receive(ref endpoint);
            ASCIIEncoding ASCII = new();
            string serverName = ASCII.GetString(data);
            // Get endpoint
            int localApiPort = localConfig.GetValue<int>("FhtWebApiPort");
            return $"https://{serverName}:{localApiPort}";
        }
        catch (Exception e)
        {
            Log.WriteErrorLine(e);
            return null;
        }
    }

    /// <summary>
    /// Get the message directory from the EMR database.
    /// </summary>
    private string GetMessageDir(string emrSoftware)
    {
        // Check override for message dir
        if (remoteConfig.MessageOutputDir != null)
        {
            return remoteConfig.MessageOutputDir;
        }

        if (emrSoftware == "BestPractice")
        {
            // Get import path for BP
            using Bps.BPSPatientsContext fhtBpsContext = new();
            Bps.Reportpaths[] reportPaths = fhtBpsContext.Reportpaths
                .Where(x => x.Recordstatus == 1 && x.Computer.Trim().ToUpper() == Environment.MachineName.Trim().ToUpper())
                .ToArray();
            if (reportPaths.Length > 0)
            {
                return reportPaths.First().Reportpath;
            }

            return null;
        }
        else if (emrSoftware == "MedicalDirector")
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

        Log.WriteLine($"Invalid EMR software '{remoteConfig.EmrSoftware}'");
        return null;
    }
}
