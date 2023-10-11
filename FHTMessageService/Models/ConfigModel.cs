namespace FHTMessageService.Models;

public record LoginInfo(string UserName, string Password);
public record UserInfo(string UserName, string Token, int AccountId);
public record ConfigRequestInfo(string ConfigurationAccountId, string ConfigurationSoftwareId);
