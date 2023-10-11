using FHTMessageService.Models;

using FhtSharedLibrary.SharedFunctions;
using FhtSharedLibrary.ViewModels;

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FHTMessageService.Client;

public class ApiClient
{
    private readonly HttpClient httpClient;

    public ApiClient(string baseAddress)
    {
        httpClient = new() { BaseAddress = new Uri(baseAddress) };
    }

    public void SetToken(string token)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T> GetJson<T>(string requestUri)
    {
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync(requestUri);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }
            else
            {
                Console.WriteLine($"Request error: {requestUri} - {response.ReasonPhrase}");
                return default;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Request error: {error}");
            return default;
        }
    }

    public async Task<T> PostJson<T>(string requestUri, object inputJson)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(requestUri, inputJson);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }
            else
            {
                Console.WriteLine($"Request error: {requestUri} - {response.ReasonPhrase}");
                return default;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"Request error: {error}");
            return default;
        }
    }

    public async Task<MessageServiceConfigModel> GetConfigInfo(int accountId, int softwareId)
    {
        try
        {
            ConfigRequestInfo configRequestInfo = new(accountId.ToString(), softwareId.ToString());
            HttpResponseMessage response = await httpClient.PostAsJsonAsync("SystemConfig", configRequestInfo);
            if (response.IsSuccessStatusCode)
            {
                MessageServiceConfigModel configResponse = await response.Content.ReadFromJsonAsync<MessageServiceConfigModel>();
                // Decrypt config strings
                if (configResponse.BpDatabaseConnectionString != null)
                    configResponse.BpDatabaseConnectionString = DecryptConfig(configResponse.BpDatabaseConnectionString);
                if (configResponse.MdDatabaseHcnConnectionString != null)
                    configResponse.MdDatabaseHcnConnectionString = DecryptConfig(configResponse.MdDatabaseHcnConnectionString);

                return configResponse;
            }
            else
            {
                Console.WriteLine($"SystemConfig error: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
        catch (Exception error)
        {
            Console.WriteLine($"SystemConfig error: {error}");
            return null;
        }
    }

    private static string DecryptConfig(string configString)
    {
        CryptoFunctions cryptoFunctions = new();
        string decryptedString = cryptoFunctions.Decrypt(configString);

        // Unable to decrypt, return original value.
        if (decryptedString.Contains("not a valid Base-64 string"))
            return configString;
        if (decryptedString.Contains("cannot be null"))
            return configString;

        return decryptedString;
    }
}
