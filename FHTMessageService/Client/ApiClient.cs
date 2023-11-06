using FHTMessageService.Logging;
using FHTMessageService.Models;

using FhtSharedLibrary.SharedFunctions;
using FhtSharedLibrary.ViewModels;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FHTMessageService.Client;

/// <summary>
/// HTTP client for communicating with local and remote API.
/// </summary>
public class ApiClient
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Create a new <see cref="ApiClient"/> for a given endpoint.
    /// </summary>
    /// <param name="baseAddress">API endpoint.</param>
    public ApiClient(string baseAddress)
    {
        httpClient = new() { BaseAddress = new Uri(baseAddress) };
    }

    /// <summary>
    /// Set the authorization header token for the HTTP client.
    /// </summary>
    public void SetToken(string token)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Make a GET request to the given endpoint and get the response as JSON.
    /// </summary>
    /// <typeparam name="T">The object type to convert the JSON to when deserializing.</typeparam>
    /// <param name="requestUri">The API endpoint to make a GET request to.</param>
    /// <returns>The returned JSON object, deserialized as type <typeparamref name="T"/>.</returns>
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
                Log.WriteErrorLine($"Request error: {requestUri} - {response.ReasonPhrase}");
                return default;
            }
        }
        catch (Exception error)
        {
            Log.WriteErrorLine($"Request error: {error}");
            return default;
        }
    }

    /// <summary>
    /// Make a POST request to the given endpoint and get the response as JSON.
    /// </summary>
    /// <typeparam name="T">The object type to convert the JSON to when deserializing.</typeparam>
    /// <typeparam name="TInput">The input object type to send with the POST request.</typeparam>
    /// <param name="requestUri">The API endpoint to make a GET request to.</param>
    /// <param name="inputJson">The value to pass when making the POST request. This is converted to JSON.</param>
    /// <returns>The returned JSON object, deserialized as type <typeparamref name="T"/>.</returns>
    public async Task<T> PostJson<T, TInput>(string requestUri, TInput inputJson)
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
                Log.WriteErrorLine($"Request error: {requestUri} - {response.ReasonPhrase}");
                return default;
            }
        }
        catch (Exception error)
        {
            Log.WriteErrorLine($"Request error: {error}");
            return default;
        }
    }

    /// <summary>
    /// Get the config info for a software system for an account.
    /// </summary>
    /// <remarks>This also decrypts the database connection strings.</remarks>
    /// <param name="accountId">Account ID to request config for.</param>
    /// <param name="softwareId">Software system ID to request config for.</param>
    /// <returns>Config info for the given account and software ID; otherwise, <see langword="null"/> if it could not be found.</returns>
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
                Log.WriteErrorLine($"GetConfigInfo error: {response.StatusCode} - {response.ReasonPhrase}");
                return null;
            }
        }
        catch (JsonException)
        {
            // No config
            return null;
        }
        catch (Exception error)
        {
            Log.WriteErrorLine($"GetConfigInfo error: {error}");
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
