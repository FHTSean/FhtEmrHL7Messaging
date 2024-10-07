using FHTMessageService.Models;

using FhtSharedLibrary.ViewModels;

using HL7.Dotnetcore;

using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace FHTMessageService.Messages;

/// <summary>
/// Utility for creating HL7 <see cref="Message"/> objects from FHT message models.
/// </summary>
public static class HL7MessageUtil
{
    public const string SoftwareProductName = "Future Health Today";
    public const string SoftwareOrganization = "The University of Melbourne";

    public const string MessageTypeCode = "ORU";
    public const string MessageTypeTriggerEvent = "R01";
    public const string MessageControlId = "0000000";
    public const string MessageVersionId = "2.8.1";
    public const string MessageCountryCode = "AU";
    public const string MessageCharacterSet = "UNICODE UTF-8";

    public const string FilenamePrefix = "fht";

    private static readonly ImmutableHashSet<string> prefixes = new HashSet<string>()
    {
        "Dr",
        "Mr",
        "Ms",
        "Mrs",
        "Mdm"
    }.ToImmutableHashSet();

    /// <summary>
    /// Create an HL7 <see cref="Message"/> from an FHT message model.
    /// </summary>
    /// <param name="messageModel">The FHT message model to convert to HL7.</param>
    /// <returns>The created HL7 <see cref="Message"/>.</returns>
    public static Message CreateHL7Message(ResultMessageModel messageModel)
    {
        Assembly application = Assembly.GetExecutingAssembly();
        string currentDateTime = MessageHelper.LongDateWithFractionOfSecond(DateTime.Now);

        // Create message
        HL7Encoding encoding = new() { SegmentDelimiter = "\r\n" };
        Message message = new() { Encoding = encoding };
        // Add header
        message.AddSegmentMSH(SoftwareProductName, SoftwareProductName, "EMR", "", "", $"{MessageTypeCode}{encoding.ComponentDelimiter}{MessageTypeTriggerEvent}", MessageControlId, "P", MessageVersionId);

        // Add software
        Segment softwareSegment = new("SFT", encoding);
        softwareSegment.AddNewField(SoftwareOrganization); // Software vendor organization
        softwareSegment.AddNewField(application.GetName().Version?.ToString()); // Software release number
        softwareSegment.AddNewField(SoftwareProductName); // Software product name
        softwareSegment.AddEmptyField(); // Software binary ID
        softwareSegment.AddNewField(application.GetName().Name); // Software product information
        softwareSegment.AddEmptyField(); // Software install date
        message.AddNewSegment(softwareSegment);

        // Add patient data
        Segment patientSegment = new("PID", encoding);
        patientSegment.AddEmptyField(); // Set ID
        patientSegment.AddEmptyField(); // Patient ID
        patientSegment.AddNewField(messageModel.Patient.PatientId.ToString()); // Patient identifier list
        patientSegment.AddEmptyField(); // Alternate patient ID
        Field patientNameField = new(encoding);
        patientNameField.AddNewComponent(new Component(messageModel.Patient.PatientFamilyName ?? "", encoding));
        patientNameField.AddNewComponent(new Component(messageModel.Patient.PatientGivenName ?? "", encoding));
        patientSegment.AddNewField(patientNameField); // Patient name
        patientSegment.AddEmptyField(); // Mother's maiden name
        patientSegment.AddNewField(MessageHelper.LongDateWithFractionOfSecond(messageModel.Patient.PatientDob)); // Date/time of birth
        patientSegment.AddNewField(messageModel.Patient.PatientSex ?? ""); // Administrative sex
        patientSegment.AddEmptyField(); // Patient alias
        patientSegment.AddEmptyField(); // Race
        patientSegment.AddNewField(messageModel.Patient.PatientAddress ?? ""); // Patient address
        message.AddNewSegment(patientSegment);

        // Add patient visit
        Segment patientVisitSegment = new("PV1", encoding);
        patientVisitSegment.AddEmptyField(); // Set ID
        patientVisitSegment.AddNewField("U"); // Patient class - Unknown
        patientVisitSegment.AddNewField(SoftwareProductName); // Assigned patient location
        patientVisitSegment.AddEmptyField(); // Admission type
        patientVisitSegment.AddEmptyField(); // Preadmit number
        patientVisitSegment.AddEmptyField(); // Prior patient location
        if (!string.IsNullOrEmpty(messageModel.PatientVisit.PatientVisitDoctor))
        {
            Field doctorNameField = new(encoding);
            DoctorName doctorName = ParseDoctorName(messageModel.PatientVisit.PatientVisitDoctor);
            doctorNameField.AddNewComponent(new Component(encoding)); // ID number
            doctorNameField.AddNewComponent(new Component(doctorName.FamilyName, encoding)); // Family name
            doctorNameField.AddNewComponent(new Component(doctorName.GivenName, encoding)); // Given name
            doctorNameField.AddNewComponent(new Component(doctorName.OtherNames, encoding)); // Further given names
            doctorNameField.AddNewComponent(new Component(encoding)); // Suffix
            doctorNameField.AddNewComponent(new Component(doctorName.Prefix, encoding)); // Prefix
            patientVisitSegment.AddNewField(doctorNameField); // Attending doctor
        }
        else
        {
            patientVisitSegment.AddEmptyField(); // Attending doctor
        }
        patientVisitSegment.AddEmptyField(); // Referring doctor
        patientVisitSegment.AddEmptyField(); // Consulting doctor
        message.AddNewSegment(patientVisitSegment);

        // Add observation request
        Segment observationRequestSegment = new("OBR", encoding);
        observationRequestSegment.AddEmptyField(); // Set ID
        observationRequestSegment.AddEmptyField(); // Placer order number
        observationRequestSegment.AddEmptyField(); // Filler order number
        Field observationRequestIndentifierField = new(encoding);
        observationRequestIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifier ?? "", encoding)); // Observation identifier
        observationRequestIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifierText ?? "", encoding)); // Observation identifier text
        observationRequestIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationCodingSystem.ToHL7CodingSystem(), encoding)); // Name of coding system - Local general code
        observationRequestSegment.AddNewField(observationRequestIndentifierField); // Observation identifier
        observationRequestSegment.AddEmptyField(); // Priority
        observationRequestSegment.AddEmptyField(); // Requested date/time
        observationRequestSegment.AddNewField(MessageHelper.LongDateWithFractionOfSecond(messageModel.Observation.ObservationDateTime)); // Observation date/time
        observationRequestSegment.AddEmptyField(); // Observation end date/time
        observationRequestSegment.AddEmptyField(); // Collection volume
        observationRequestSegment.AddEmptyField(); // Collector identifier
        observationRequestSegment.AddEmptyField(); // Specimen action code
        observationRequestSegment.AddEmptyField(); // Danger code
        observationRequestSegment.AddEmptyField(); // Relevant clinical information
        observationRequestSegment.AddEmptyField(); // Specimen received date/time
        observationRequestSegment.AddEmptyField(); // Specimen source
        observationRequestSegment.AddEmptyField(); // Ordering provider
        observationRequestSegment.AddEmptyField(); // Order callback phone number
        observationRequestSegment.AddEmptyField(); // Placer field 1
        observationRequestSegment.AddEmptyField(); // Placer field 2
        observationRequestSegment.AddEmptyField(); // Filler field 1
        observationRequestSegment.AddEmptyField(); // Filler field 2
        observationRequestSegment.AddNewField(currentDateTime); // Results report date/time
        observationRequestSegment.AddEmptyField(); // Charge to practice
        observationRequestSegment.AddEmptyField(); // Diagnostic service section ID
        observationRequestSegment.AddNewField("F"); // Result status - Final results
        message.AddNewSegment(observationRequestSegment);

        // Add observation result
        Segment observationResultSegment = new("OBX", encoding);
        observationResultSegment.AddEmptyField(); // Set ID
        observationResultSegment.AddNewField("NM"); // Value type - Numeric
        Field observationIndentifierField = new(encoding);
        observationIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifier ?? "", encoding)); // Observation identifier
        observationIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifierText ?? "", encoding)); // Observation identifier text
        observationIndentifierField.AddNewComponent(new Component(messageModel.Observation.ObservationCodingSystem.ToHL7CodingSystem(), encoding)); // Name of coding system - Local general code
        observationResultSegment.AddNewField(observationIndentifierField); // Observation identifier
        observationResultSegment.AddEmptyField(); // Observation sub-identifier
        observationResultSegment.AddNewField(messageModel.Observation.ObservationValue ?? ""); // Observation value
        observationResultSegment.AddNewField(messageModel.Observation.ObservationUnits ?? ""); // Units
        observationResultSegment.AddNewField(messageModel.Observation.ObservationReferencesRange ?? ""); // References range
        observationResultSegment.AddNewField(messageModel.Observation.ObservationAbnormalFlags ?? ""); // Abnormal flags
        observationResultSegment.AddEmptyField(); // Probability
        observationResultSegment.AddEmptyField(); // Nature of abnormal test
        observationResultSegment.AddNewField("F"); // Observation result status - Final results
        observationResultSegment.AddEmptyField(); // Effective date of reference range values
        observationResultSegment.AddEmptyField(); // User defined access checks
        observationResultSegment.AddNewField(currentDateTime); // Date/time of the observation
        observationResultSegment.AddEmptyField(); // Producer's reference
        observationResultSegment.AddEmptyField(); // Responsible observer
        observationResultSegment.AddNewField(SoftwareProductName); // Observation method
        observationResultSegment.AddEmptyField(); // Equipment instance identifier
        observationResultSegment.AddNewField(currentDateTime); // Date/time of analysis
        message.AddNewSegment(observationResultSegment);

        // Add formatted text
        Segment formattedTextSegment = new("OBX", encoding);
        formattedTextSegment.AddEmptyField(); // Set ID
        formattedTextSegment.AddNewField("FT"); // Value type - Formatted text
        formattedTextSegment.AddNewField("DS"); // Observation identifier
        formattedTextSegment.AddEmptyField(); // Observation sub-identifier
        formattedTextSegment.AddNewField(messageModel.FormattedText ?? ""); // Observation value
        formattedTextSegment.AddEmptyField(); // Units
        formattedTextSegment.AddEmptyField(); // References range
        formattedTextSegment.AddEmptyField(); // Abnormal flags
        formattedTextSegment.AddEmptyField(); // Probability
        formattedTextSegment.AddEmptyField(); // Nature of abnormal test
        formattedTextSegment.AddNewField("F"); // Observation result status - Final results
        formattedTextSegment.AddEmptyField(); // Effective date of reference range values
        formattedTextSegment.AddEmptyField(); // User defined access checks
        formattedTextSegment.AddNewField(currentDateTime); // Date/time of the observation
        message.AddNewSegment(formattedTextSegment);

        // Add clinical trial identification
        Segment clinicalTrialIdentificationSegment = new("CTI", encoding);
        clinicalTrialIdentificationSegment.AddNewField(messageModel.ClinicalTrial.StudyIdentifier ?? ""); // Sponser study ID
        Field clinicalTrialStudyPhaseField = new(encoding);
        clinicalTrialStudyPhaseField.AddNewComponent(new Component(messageModel.ClinicalTrial.StudyPhaseIdentifier ?? "", encoding));
        clinicalTrialStudyPhaseField.AddNewComponent(new Component(messageModel.ClinicalTrial.StudyPhaseIdentifierText ?? "", encoding));
        clinicalTrialIdentificationSegment.AddNewField(clinicalTrialStudyPhaseField); // Study phase identifier
        message.AddNewSegment(clinicalTrialIdentificationSegment);

        return message;
    }

    private static DoctorName ParseDoctorName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        string prefix = "";
        string givenName = "";
        string otherNames = "";
        string familyName = "";

        int index = 0;
        string[] nameComponents = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Prefix
        if (nameComponents.Length > index)
        {
            string prefixComponent = nameComponents[index];
            if (prefixes.Any(x => prefixComponent.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                prefix = prefixComponent;
                ++index;
            }
        }

        // Given name
        if (nameComponents.Length > index)
        {
            givenName = nameComponents[index];
            ++index;
        }

        // Other names
        if (nameComponents.Length - 1 > index)
        {
            string[] otherNamesComponents = nameComponents[index..(nameComponents.Length - 1)];
            otherNames = string.Join(' ', otherNamesComponents);
            index += otherNamesComponents.Length;
        }

        // Family name
        if (nameComponents.Length > index)
        {
            familyName = nameComponents[index];
            ++index;
        }

        return new(familyName, givenName, otherNames, prefix);
    }

    /// <summary>
    /// Convert special characters to display correctly in MD.
    /// </summary>
    /// <param name="text">The input text to convert.</param>
    /// <returns>The <paramref name="text"/> converted to display in MD.</returns>
    public static string MedicalDirectorTextConversion(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder textBuilder = new();
        foreach (char c in text)
        {
            // Convert 8-bit characters to MD format
            if (c >= 0b10000000)
                textBuilder.Append($"\\'{Convert.ToByte(c).ToString("x2").ToLowerInvariant()}");
            else
                textBuilder.Append(c);
        }

        return textBuilder.ToString();
    }

    /// <summary>
    /// Get a unique filename from an FHT message model.
    /// </summary>
    public static string CreateFilenameFromMessage(ResultMessageModel messageModel)
    {
        return $"{FilenamePrefix}_{messageModel.Patient.PatientId}_{messageModel.Observation.ObservationIdentifierText}_{DateTime.Now.Ticks}.hl7"
            .Replace(" ", "")
            .ToLower();
    }

    /// <summary>
    /// Write an HL7 <see cref="Message"/> to a file.
    /// </summary>
    /// <param name="message">The message to write to a file.</param>
    /// <param name="messagePath">The filepath for the HL7 file.</param>
    public static void WriteHL7Message(Message message, string messagePath, string messageEmr)
    {
        // Create HL7 message
        string serializedMessage = message.SerializeMessage(false);
        // Format HL7 message
        string formattedText;
        if (messageEmr == "MedicalDirector")
            formattedText = MedicalDirectorTextConversion(serializedMessage);
        else
            formattedText = serializedMessage;

        // Check dir
        string messageDir = Path.GetDirectoryName(messagePath);
        if (!Directory.Exists(messageDir))
        {
            Directory.CreateDirectory(messageDir);
        }

        // Write file
        using FileStream fileStream = new(messagePath, FileMode.Create);
        using StreamWriter messageWriter = new(fileStream, Encoding.Latin1);
        messageWriter.Write(formattedText);
    }
}
