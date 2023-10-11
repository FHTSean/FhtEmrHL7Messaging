using FhtSharedLibrary.ViewModels;

using HL7.Dotnetcore;

using System.Reflection;

namespace FHTMessageService.Messages;

public static class HL7MessageUtil
{
    public const string SOFTWARE_PRODUCT_NAME = "Future Health Today";
    public const string SOFTWARE_ORGANIZATION = "The University of Melbourne";

    public const string MESSAGE_TYPE_CODE = "REF";
    public const string MESSAGE_TYPE_TRIGGER_EVENT = "I12";
    public const string MESSAGE_CONTROL_ID = "0000000";
    public const string MESSAGE_VERSION_ID = "2.5.1";
    public const string MESSAGE_COUNTRY_CODE = "AU";
    public const string MESSAGE_CHARACTER_SET = "UNICODE UTF-8";

    public static Message CreateHL7Message(ResultMessageModel messageModel)
    {
        Assembly application = Assembly.GetExecutingAssembly();
        string currentDateTime = MessageHelper.LongDateWithFractionOfSecond(DateTime.Now);

        // Create message
        HL7Encoding encoding = new() { SegmentDelimiter = "\r\n" };
        Message message = new() { Encoding = encoding };
        // Add header
        message.AddSegmentMSH(SOFTWARE_PRODUCT_NAME, SOFTWARE_PRODUCT_NAME, "EMR", "", "", $"{MESSAGE_TYPE_CODE}{encoding.ComponentDelimiter}{MESSAGE_TYPE_TRIGGER_EVENT}", MESSAGE_CONTROL_ID, "P", MESSAGE_VERSION_ID);

        // Add software
        Segment softwareSegment = new("SFT", encoding);
        softwareSegment.AddNewField(SOFTWARE_ORGANIZATION); // Software vendor organization
        softwareSegment.AddNewField(application.GetName().Version?.ToString()); // Software release number
        softwareSegment.AddNewField(SOFTWARE_PRODUCT_NAME); // Software product name
        softwareSegment.AddEmptyField(); // Software binary ID
        softwareSegment.AddNewField(application.GetName().Name); // Software product information
        softwareSegment.AddEmptyField(); // Software install date
        message.AddNewSegment(softwareSegment);

        // Add referral information
        Segment referralSegment = new("RF1", encoding);
        referralSegment.AddNewField("P"); // Referral status - pending
        referralSegment.AddNewField("R"); // Referral priority - routine
        referralSegment.AddNewField("Med"); // Referral type - medical
        message.AddNewSegment(referralSegment);

        // Add provider data
        Segment providerDataSegment = new("PRD", encoding);
        providerDataSegment.AddNewField("RP"); // Provider role - referring provider
        providerDataSegment.AddNewField(SOFTWARE_PRODUCT_NAME); // Provider name
        message.AddNewSegment(providerDataSegment);

        // Add patient data
        Segment patientSegment = new("PID", encoding);
        patientSegment.AddEmptyField(); // Set ID
        patientSegment.AddNewField(messageModel.Patient.PatientId); // Patient ID
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

        // Add observation request
        Segment observationRequestSegment = new("OBR", encoding);
        observationRequestSegment.AddEmptyField(); // Set ID
        observationRequestSegment.AddEmptyField(); // Placer order number
        observationRequestSegment.AddEmptyField(); // Filler order number
        observationRequestSegment.AddEmptyField(); // Universal service identifier
        observationRequestSegment.AddEmptyField(); // Priority
        observationRequestSegment.AddEmptyField(); // Requested date/time
        observationRequestSegment.AddEmptyField(); // Observation date/time
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
        message.AddNewSegment(observationRequestSegment);

        // Add observation result
        Segment observationResultSegment = new("OBX", encoding);
        observationResultSegment.AddEmptyField(); // Set ID
        observationResultSegment.AddNewField("NM"); // Value type - numeric
        observationResultSegment.AddNewField(messageModel.Observation.ObservationIdentifier); // Observation identifier
        observationResultSegment.AddEmptyField(); // Observation sub-identifier
        observationResultSegment.AddNewField(messageModel.Observation.ObservationValue); // Observation value
        observationResultSegment.AddNewField(messageModel.Observation.ObservationUnits); // Units
        observationResultSegment.AddNewField(messageModel.Observation.ObservationReferencesRange); // References range
        observationResultSegment.AddNewField(messageModel.Observation.ObservationAbnormalFlags); // Abnormal flags
        observationResultSegment.AddEmptyField(); // Probability
        observationResultSegment.AddEmptyField(); // Nature of abnormal test
        observationResultSegment.AddEmptyField(); // Observation result status
        observationResultSegment.AddEmptyField(); // Effective date of reference range values
        observationResultSegment.AddEmptyField(); // User defined access checks
        observationResultSegment.AddNewField(currentDateTime); // Date/time of the observation
        observationResultSegment.AddEmptyField(); // Producer's reference
        observationResultSegment.AddEmptyField(); // Responsible observer
        observationResultSegment.AddEmptyField(); // Observation method
        observationResultSegment.AddEmptyField(); // Equipment instance identifier
        observationResultSegment.AddNewField(currentDateTime); // Date/time of analysis
        message.AddNewSegment(observationResultSegment);

        // Add patient visit
        Segment patientVisitSegment = new("PV1", encoding);
        patientVisitSegment.AddEmptyField(); // Set ID
        patientVisitSegment.AddEmptyField(); // Patient class
        patientVisitSegment.AddEmptyField(); // Assigned patient location
        patientVisitSegment.AddEmptyField(); // Admission type
        patientVisitSegment.AddEmptyField(); // Preadmit number
        patientVisitSegment.AddEmptyField(); // Prior patient location
        patientVisitSegment.AddEmptyField(); // Attending doctor
        patientVisitSegment.AddEmptyField(); // Referring doctor
        patientVisitSegment.AddNewField(messageModel.PatientVisit.PatientVisitDoctor); // Consulting doctor
        message.AddNewSegment(patientVisitSegment);

        return message;
    }

    public static string CreateFilenameFromMessage(ResultMessageModel messageModel)
    {
        return $"{messageModel.Patient.PatientId}_{messageModel.Observation.ObservationIdentifier}_{DateTime.Now.Ticks}.hl7";
    }

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
