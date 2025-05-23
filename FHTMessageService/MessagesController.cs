﻿using FHTMessageService.Client;
using FHTMessageService.Logging;
using FHTMessageService.Messages;
using FHTMessageService.Models;

using FhtSharedLibrary.SharedFunctions;

using FhtSharedLibrary.ViewModels;

using HL7.Dotnetcore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using Bps = FhtSharedLibrary.EntityModels.BestPracticeJadeSP2.BPSPatients;
using MdPath = FhtSharedLibrary.EntityModels.MD3;

namespace FHTMessageService;

[ApiController]
public class MessagesController : ControllerBase
{
    private const int TimeoutMilliseconds = 30000;

    [Route("ResultMessages")]
    public async Task Get()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await GetResultMessagesFromSocket(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private async Task GetResultMessagesFromSocket(WebSocket webSocket)
    {
        StringBuilder resultBuilder = new();
        bool isClosed = false;

        try
        {
            while (!isClosed)
            {
                byte[] buffer = new byte[1024 * 4];
                WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), new CancellationTokenSource(TimeoutMilliseconds).Token);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    isClosed = true;
                    break;
                }
                else if (receiveResult.MessageType != WebSocketMessageType.Text)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, null, new CancellationTokenSource(TimeoutMilliseconds).Token);
                    break;
                }

                resultBuilder.Append(Encoding.UTF8.GetString(buffer));
                if (receiveResult.EndOfMessage)
                {
                    // Get result
                    string result = resultBuilder.ToString();
                    resultBuilder.Clear();
                    // Parse and save result
                    ResultMessageModel[] resultMessageModels = JsonSerializer.Deserialize<ResultMessageModel[]>(result.Trim('\0'));
                    await SaveResultMessages(resultMessageModels, webSocket);
                }
            }

            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, new CancellationTokenSource(TimeoutMilliseconds).Token);
        }
        catch (Exception e)
        {
            // Send error over socket
            await LogWebsocketMessage(webSocket, e.ToString());
            await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, null, new CancellationTokenSource(TimeoutMilliseconds).Token);
        }
    }

    private static async Task LogWebsocketMessage(WebSocket webSocket, string value)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(value);
        await webSocket.SendAsync(messageBytes, WebSocketMessageType.Text, true, new CancellationTokenSource(TimeoutMilliseconds).Token);
    }

    private static async Task SaveResultMessages(ResultMessageModel[] resultMessageModels, WebSocket webSocket)
    {
        ArgumentNullException.ThrowIfNull(resultMessageModels);

        Log.WriteLine("Result messages received", LogFormat.Bold, LogFormat.Italic);
        Log.WriteLine($"Saving {resultMessageModels.Length} result messages");
        // Parse configs
        MessageServiceConfigModel remoteConfig = null;
        IConfigurationRoot localConfig = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        Log.WriteLine("Local config obtained");
        Log.WriteLine();

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
        using StreamReader clientConfigReader = System.IO.File.OpenText(clientConfigPath);
        string[] clientConfigLines = clientConfigReader.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        string[] configUser = clientConfigLines[1].Split(["User="], 2, StringSplitOptions.None);
        string username = configUser.Length == 2 ? configUser[1] : null;
        string[] configPassword = clientConfigLines[2].Split(["Password="], 2, StringSplitOptions.None);
        string password = configPassword.Length == 2 ? cryptoDecrypt.Decrypt(configPassword[1]) : null;
        Log.Write("Username: ", LogFormat.Bold);
        Log.Write(username);
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
            Log.WriteLine("Remote login successful", LogFormat.BrightGreen);
            Log.Write("Username: ", LogFormat.Bold);
            Log.Write(remoteUserInfo.UserName);
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
            Log.WriteLine("Remote config obtained");
        }
        else
        {
            // Use empty config if no config exists
            Log.WriteLine("Could not find remote config, using default values", LogFormat.Yellow);
            remoteConfig = new MessageServiceConfigModel();
        }

        // Get connection strings from configs
        Dictionary<string, string> emrConnectionStrings = new()
        {
            { "BestPractice", remoteConfig.BpDatabaseConnectionString ?? localConfig.GetConnectionString("BpDatabaseConnectionString") },
            { "MedicalDirector", remoteConfig.MdDatabaseHcnConnectionString ?? localConfig.GetConnectionString("MdDatabaseHcnConnectionString") },
        };

        // Get message from local API
        ResultMessageModel[] messageModels = resultMessageModels;

        // Iterate through each message
        if (messageModels != null && messageModels.Length > 0)
        {
            // Get message path
            Dictionary<string, string> messageDirs = messageModels
                .Select(x => x.Patient.PatientEmr).Distinct()
                // .ToDictionary(x => x, y => GetMessageDir(y, emrConnectionStrings[y]));
                .ToDictionary(x => x, y => remoteConfig.MessageOutputDir ?? GetMessageDir());
            if (messageDirs.Count != 0)
            {
                foreach (string emrSoftware in messageDirs.Keys)
                {
                    Log.Write($"{emrSoftware} - Message dir: ", LogFormat.Bold);
                    Log.WriteLine(messageDirs[emrSoftware]);
                    await LogWebsocketMessage(webSocket, $"EMR: '{emrSoftware}' - Message dir: '{messageDirs[emrSoftware]}'");
                }
            }
            else
            {
                Log.WriteErrorLine("Could not find message dir");
                throw new Exception("Could not find message dir");
            }

            Log.WriteLine();
            Log.WriteLine($"{messageModels.Length} {(messageModels.Length == 1 ? "message" : "messages")} from API");

            int wroteMessages = 0;
            int wroteSilentMessages = 0;
            int failedMessages = 0;
            foreach (ResultMessageModel messageModel in messageModels)
            {
                try
                {
                    // Write message to emr
                    Message message = HL7MessageUtil.CreateHL7Message(messageModel);
                    string messageFilename = HL7MessageUtil.CreateFilenameFromMessage(messageModel);
                    string messagePath = Path.Join(messageDirs[messageModel.Patient.PatientEmr], messageFilename);
                    if (!messageModel.IsSilent)
                    {
                        HL7MessageUtil.WriteHL7Message(message, messagePath, messageModel.Patient.PatientEmr);
                        Log.WriteLine(messagePath);
                        ++wroteMessages;
                    }
                    else
                    {
                        Log.WriteLine($"{messagePath} (Silent)");
                        ++wroteSilentMessages;
                    }
                }
                catch (Exception e)
                {
                    Log.WriteErrorLine(e);
                    ++failedMessages;
                }
            }

            if (wroteMessages > 0)
            {
                string message = $"Successfully wrote {wroteMessages} {(wroteMessages == 1 ? "message" : "messages")}";
                Log.WriteLine(message, LogFormat.BrightGreen);
                await LogWebsocketMessage(webSocket, message);
            }

            if (wroteSilentMessages > 0)
            {
                string message = $"Successfully wrote {wroteSilentMessages} silent {(wroteSilentMessages == 1 ? "message" : "messages")}";
                Log.WriteLine(message, LogFormat.Green);
                await LogWebsocketMessage(webSocket, message);
            }

            if (failedMessages > 0)
            {
                Log.WriteLine($"Failed to write {failedMessages} {(failedMessages == 1 ? "message" : "messages")}", LogFormat.BrightYellow);
            }
        }
        else
        {
            Log.WriteLine("No messages to write");
        }

        Log.WriteLine();
    }

    public static string GetMessageDir()
    {
        return "C:\\Program Files\\fht\\FHTMessageService\\HL7Reports";
    }

    /// <summary>
    /// Get the message directory from the EMR database.
    /// </summary>
    public static string GetMessageDir(string emrSoftware, string connectionString)
    {
        if (emrSoftware == "BestPractice")
        {
            DbContextOptionsBuilder<Bps.BPSPatientsContext> optionsBuilder = new();
            optionsBuilder.UseSqlServer(connectionString);
            using Bps.BPSPatientsContext fhtBpsContext = new(optionsBuilder.Options);
            // Get import path for BP
            Bps.Reportpaths[] reportPaths = fhtBpsContext.Reportpaths
                .AsEnumerable()
                .OrderBy(x => x.Recordid)
                .Where(x => x.Recordstatus == 1 && x.Computer.Trim().Equals(Environment.MachineName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (Bps.Reportpaths path in reportPaths)
            {
                Log.WriteLine(path.ToString());
            }

            if (reportPaths.Length > 0)
            {
                return reportPaths.First().Reportpath;
            }

            throw new Exception($"No matching report paths found for machine '{Environment.MachineName}'");
        }
        else if (emrSoftware == "MedicalDirector")
        {
            DbContextOptionsBuilder<MdPath.HCN.HCNContext> optionsBuilder = new();
            optionsBuilder.UseSqlServer(connectionString);
            using MdPath.HCN.HCNContext fhtMdContext = new(optionsBuilder.Options);
            // Get import path for MD
            MdPath.HCN.MdUpdownConfig[] reportPaths = [.. fhtMdContext.MdUpdownConfig.Where(x => x.Enabled == "Y" && x.SdiEnabled == "Y" && x.StampActionCode != "D")];
            if (reportPaths.Length > 0)
            {
                return reportPaths.First().ImportDirectory;
            }

            throw new Exception($"No matching report paths found for machine '{Environment.MachineName}'");
        }

        throw new ArgumentException($"Invalid EMR software '{emrSoftware}'", nameof(emrSoftware));
    }
}
