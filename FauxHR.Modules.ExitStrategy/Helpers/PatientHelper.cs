using Hl7.Fhir.Model;

namespace FauxHR.Modules.ExitStrategy.Helpers;

public static class PatientHelper
{
    public static string GetPatientName(Patient? patient)
    {
        if (patient?.Name?.FirstOrDefault() is HumanName name)
        {
            var given = string.Join(" ", name.Given ?? Array.Empty<string>());
            return $"{given} {name.Family}".Trim();
        }
        return "Unknown";
    }

    public static (bool? isCapable, string? comment) GetLegallyCapableInfo(Patient? patient)
    {
        if (patient == null) return (null, null);

        var ext = patient.Extension?.FirstOrDefault(e =>
            e.Url == "https://api.iknl.nl/docs/pzp/r4/StructureDefinition/ext-LegallyCapable-MedicalTreatmentDecisions");

        if (ext == null) return (null, null);

        var capableExt = ext.Extension?.FirstOrDefault(e => e.Url == "legallyCapable");
        var commentExt = ext.Extension?.FirstOrDefault(e => e.Url == "legallyCapableComment");

        bool? capable = capableExt?.Value is FhirBoolean fb ? fb.Value : null;
        string? comment = commentExt?.Value is FhirString fs ? fs.Value : null;

        return (capable, comment);
    }

    public static string GetLegallyCapableText(Patient? patient)
    {
        var (capable, _) = GetLegallyCapableInfo(patient);
        return capable switch
        {
            true => "Wilsbekwaam",
            false => "Niet wilsbekwaam",
            null => "Status onbekend"
        };
    }

    public static string GetLegallyCapableComment(Patient? patient)
    {
        var (_, comment) = GetLegallyCapableInfo(patient);
        return comment ?? string.Empty;
    }

    public static string GetLegallyCapableClass(Patient? patient)
    {
        var (capable, _) = GetLegallyCapableInfo(patient);
        return capable switch
        {
            true => "bg-success-subtle border-success text-success-emphasis",
            false => "bg-danger-subtle border-danger text-danger-emphasis",
            null => "bg-warning-subtle border-warning text-warning-emphasis"
        };
    }

    public static string GetLegallyCapableIcon(Patient? patient)
    {
        var (capable, _) = GetLegallyCapableInfo(patient);
        return capable switch
        {
            true => "bi bi-check-circle-fill text-success",
            false => "bi bi-x-circle-fill text-danger",
            null => "bi bi-question-circle-fill text-warning"
        };
    }
}
