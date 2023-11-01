namespace FHTMessageService.Models;

/// <summary>
/// Data for login requests.
/// </summary>
public record LoginInfo(string UserName, string Password);
/// <summary>
/// Result data from login requests.
/// </summary>
public record UserInfo(string UserName, string Token, int AccountId);
/// <summary>
/// Data for remote config requests.
/// </summary>
public record ConfigRequestInfo(string ConfigurationAccountId, string ConfigurationSoftwareId);
