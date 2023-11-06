namespace FHTMessageService.Models;

/// <summary>
/// Data for parsed doctor names.
/// </summary>
public record DoctorName(string FamilyName, string GivenName, string OtherNames, string Prefix);
