using Hl7.Fhir.Model;

namespace FauxHR.Modules.ExitStrategy.Models;

public class AcpEncounterViewModel
{
    public Encounter Encounter { get; set; } = new();
    public Procedure? Procedure { get; set; }
    public DateTime Date { get; set; }
    public List<ParticipantInfo> Participants { get; set; } = new();
    public List<QuestionnaireResponse> QuestionnaireResponses { get; set; } = new();
    public List<Observation> Observations { get; set; } = new();
}

public record ParticipantInfo(string Display, string Reference, bool IsPractitioner, string? Role);

public class ParticipantDetail 
{
    public string Name { get; set; } = "";
    public string Reference { get; set; } = "";
    public string ResourceType { get; set; } = "";
    public string? Role { get; set; }
    public string? Specialty { get; set; }
    public string? Organization { get; set; }
    public string? Relationship { get; set; }
    public List<ContactPoint> Telecoms { get; set; } = new();
}
