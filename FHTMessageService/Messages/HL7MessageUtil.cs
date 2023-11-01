using FhtSharedLibrary.ViewModels;

using HL7.Dotnetcore;

using System.Reflection;

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
    public const string MessageVersionId = "2.5.1";
    public const string MessageCountryCode = "AU";
    public const string MessageCharacterSet = "UNICODE UTF-8";

    public const string FilenamePrefix = "fht";

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
        patientSegment.AddNewField(messageModel.Patient.PatientId); // Patient identifier list
        patientSegment.AddEmptyField(); // Alternate patient ID
        Field patientName = new(encoding);
        patientName.AddNewComponent(new Component(messageModel.Patient.PatientFamilyName, encoding));
        patientName.AddNewComponent(new Component(messageModel.Patient.PatientGivenName, encoding));
        patientSegment.AddNewField(patientName); // Patient name
        patientSegment.AddEmptyField(); // Mother's maiden name
        patientSegment.AddNewField(MessageHelper.LongDateWithFractionOfSecond(messageModel.Patient.PatientDob)); // Date/time of birth
        patientSegment.AddNewField(messageModel.Patient.PatientSex); // Administrative sex
        patientSegment.AddEmptyField(); // Patient alias
        patientSegment.AddEmptyField(); // Race
        patientSegment.AddNewField(messageModel.Patient.PatientAddress); // Patient address
        message.AddNewSegment(patientSegment);

        // Add patient visit
        Segment patientVisitSegment = new("PV1", encoding);
        patientVisitSegment.AddEmptyField(); // Set ID
        patientVisitSegment.AddNewField("U"); // Patient class - Unknown
        patientVisitSegment.AddNewField(SoftwareProductName); // Assigned patient location
        patientVisitSegment.AddEmptyField(); // Admission type
        patientVisitSegment.AddEmptyField(); // Preadmit number
        patientVisitSegment.AddEmptyField(); // Prior patient location
        patientVisitSegment.AddNewField(messageModel.PatientVisit.PatientVisitDoctor); // Attending doctor
        patientVisitSegment.AddEmptyField(); // Referring doctor
        patientVisitSegment.AddEmptyField(); // Consulting doctor
        message.AddNewSegment(patientVisitSegment);

        // Add observation request
        Segment observationRequestSegment = new("OBR", encoding);
        observationRequestSegment.AddEmptyField(); // Set ID
        observationRequestSegment.AddEmptyField(); // Placer order number
        observationRequestSegment.AddEmptyField(); // Filler order number
        observationRequestSegment.AddEmptyField(); // Universal service identifier
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
        Field observationIndentifier = new(encoding);
        observationIndentifier.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifier, encoding)); // Observation identifier
        observationIndentifier.AddNewComponent(new Component(messageModel.Observation.ObservationIdentifierText, encoding)); // Observation identifier text
        observationIndentifier.AddNewComponent(new Component(messageModel.Observation.ObservationCodingSystem.ToHL7CodingSystem(), encoding)); // Name of coding system - Local general code
        observationResultSegment.AddNewField(observationIndentifier); // Observation identifier
        observationResultSegment.AddEmptyField(); // Observation sub-identifier
        observationResultSegment.AddNewField(messageModel.Observation.ObservationValue); // Observation value
        observationResultSegment.AddNewField(messageModel.Observation.ObservationUnits); // Units
        observationResultSegment.AddNewField(messageModel.Observation.ObservationReferencesRange); // References range
        observationResultSegment.AddNewField(messageModel.Observation.ObservationAbnormalFlags); // Abnormal flags
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

        // Add clinical trial identification
        Segment clinicalTrialIdentificationSegment = new("CTI", encoding);
        clinicalTrialIdentificationSegment.AddNewField(messageModel.ClinicalTrial.StudyIdentifier); // Sponser study ID
        Field clinicalTrialStudyPhase = new(encoding);
        clinicalTrialStudyPhase.AddNewComponent(new Component(messageModel.ClinicalTrial.StudyPhaseIdentifier, encoding));
        clinicalTrialStudyPhase.AddNewComponent(new Component(messageModel.ClinicalTrial.StudyPhaseIdentifierText, encoding));
        clinicalTrialIdentificationSegment.AddNewField(clinicalTrialStudyPhase); // Study phase identifier
        message.AddNewSegment(clinicalTrialIdentificationSegment);

        return message;
    }

    /// <summary>
    /// Get a unique filename from an FHT message model.
    /// </summary>
    public static string CreateFilenameFromMessage(ResultMessageModel messageModel)
    {
        return $"{FilenamePrefix}_{messageModel.Patient.PatientId}_{messageModel.Observation.ObservationIdentifier}_{DateTime.Now.Ticks}.hl7";
    }

    /// <summary>
    /// Write an HL7 <see cref="Message"/> to a file.
    /// </summary>
    /// <param name="message">The message to write to a file.</param>
    /// <param name="messagePath">The filepath for the HL7 file.</param>
    public static void WriteHL7Message(Message message, string messagePath)
    {
        // Create HL7 message
        string hl7Message = message.SerializeMessage(false);
        // Check dir
        string messageDir = Path.GetDirectoryName(messagePath);
        if (!Directory.Exists(messageDir))
        {
            Directory.CreateDirectory(messageDir);
        }

        // Write file
        using StreamWriter messageWriter = new(messagePath);
        messageWriter.Write(hl7Message);
    }
}
