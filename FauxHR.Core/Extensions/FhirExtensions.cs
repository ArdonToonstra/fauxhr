using Hl7.Fhir.Model;

namespace FauxHR.Core.Extensions;

public static class FhirExtensions
{
    public static string ToHumanName(this Patient patient)
    {
        if (patient == null) return "Unknown Patient";
        
        var name = patient.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official) ?? patient.Name.FirstOrDefault();
        
        if (name == null) return "No Name";

        return $"{name.Given.FirstOrDefault()} {name.Family}".Trim();
    }
}
