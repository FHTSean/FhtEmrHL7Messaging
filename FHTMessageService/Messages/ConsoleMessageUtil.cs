using FhtSharedLibrary.ViewModels;

namespace FHTMessageService.Messages;

public static class ConsoleMessageUtil
{
    public static ResultMessageModel CreateResultMessageFromConsoleInput()
    {
        Console.WriteLine("Creating HL7 message from console input");
        // Get data
        Console.Write("Patient ID: ");
        string patientId = Console.ReadLine();
        Console.Write("Patient family name: ");
        string patientFamilyName = Console.ReadLine();
        Console.Write("Patient given name: ");
        string patientGivenName = Console.ReadLine();
        Console.Write("Patient DOB: ");
        DateTime patientDob = DateTime.Parse(Console.ReadLine());
        Console.Write("Patient Sex: ");
        string patientSex = Console.ReadLine();
        Console.Write("Patient address: ");
        string patientAddress = Console.ReadLine();

        Console.Write("Observation identifier: ");
        string observationIdentifier = Console.ReadLine();
        Console.Write("Observation value: ");
        string observationValue = Console.ReadLine();
        Console.Write("Observation units: ");
        string observationUnits = Console.ReadLine();
        Console.Write("Observation references range: ");
        string observationReferencesRange = Console.ReadLine();
        Console.Write("Observation abnormal flags: ");
        string observationAbnormalFlags = Console.ReadLine();

        Console.Write("Consulting doctor: ");
        string patientVisitDoctor = Console.ReadLine();

        return new()
        {
            Patient = new()
            {
                PatientId = patientId,
                PatientFamilyName = patientFamilyName,
                PatientGivenName = patientGivenName,
                PatientDob = patientDob,
                PatientSex = patientSex,
                PatientAddress = patientAddress,
            },
            Observation = new()
            {
                ObservationIdentifier = observationIdentifier,
                ObservationValue = observationValue,
                ObservationUnits = observationUnits,
                ObservationReferencesRange = observationReferencesRange,
                ObservationAbnormalFlags = observationAbnormalFlags,
            },
            PatientVisit = new()
            {
                PatientVisitDoctor = patientVisitDoctor,
            },
        };
    }
}
