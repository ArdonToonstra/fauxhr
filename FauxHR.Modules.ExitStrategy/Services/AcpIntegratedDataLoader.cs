using Blazored.LocalStorage;
using FauxHR.Modules.ExitStrategy.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using FauxHR.Modules.ExitStrategy.Helpers;
using Task = System.Threading.Tasks.Task;

namespace FauxHR.Modules.ExitStrategy.Services;

public class AcpIntegratedData
{
    public Patient? CurrentPatient { get; set; }
    public List<AcpEncounterViewModel> AcpEncounters { get; set; } = new();
    public List<QuestionnaireResponse> UnlinkedQuestionnaires { get; set; } = new();
    public List<Goal> PatientGoals { get; set; } = new();
    public Goal? LatestGoal { get; set; }
    public List<Consent> AllConsents { get; set; } = new();
    public List<TreatmentDirectiveViewModel> Permits { get; set; } = new();
    public List<TreatmentDirectiveViewModel> Denials { get; set; } = new();
    public List<TreatmentDirectiveViewModel> Others { get; set; } = new();
    public List<Observation> AllObservations { get; set; } = new();
    public List<Observation> LatestObservations { get; set; } = new();
}

public class AcpIntegratedDataLoader
{
    private readonly ILocalStorageService _localStorage;
    private readonly FhirJsonDeserializer _parser = new();
    
    private readonly string[] _allowedGoalCodes = new[] { "385987000", "1351964001", "713148004" };
    private readonly string[] _allowedObservationCodes = new[] { "153851000146100", "395091006", "340171000146104", "247751003" };

    public AcpIntegratedDataLoader(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<AcpIntegratedData> LoadDataAsync(Patient? currentPatient)
    {
        var data = new AcpIntegratedData();
        
        if (currentPatient == null)
            return data;

        // Load patient data
        data.CurrentPatient = await LoadPatientDataAsync(currentPatient);
        
        var keys = await _localStorage.KeysAsync();

        // Load all resources
        var allProcedures = new List<Procedure>();
        var allEncounters = new List<Encounter>();
        var allQuestionnaireResponses = new List<QuestionnaireResponse>();

        foreach (var key in keys)
        {
            if (key.Contains("Procedure") && !key.Contains("QuestionnaireResponse"))
            {
                await TryLoadResourceAsync<Procedure>(key, allProcedures.Add);
            }
            else if (key.Contains("Encounter"))
            {
                await TryLoadResourceAsync<Encounter>(key, allEncounters.Add);
            }
            else if (key.Contains("QuestionnaireResponse"))
            {
                await TryLoadResourceAsync<QuestionnaireResponse>(key, allQuestionnaireResponses.Add);
            }
            else if (key.Contains("Observation"))
            {
                await TryLoadResourceAsync<Observation>(key, data.AllObservations.Add);
            }
            else if (key.Contains("Goal"))
            {
                await TryLoadResourceAsync<Goal>(key, g => OnGoalLoaded(g, data.PatientGoals));
            }
            else if (key.Contains("Consent"))
            {
                await TryLoadResourceAsync<Consent>(key, data.AllConsents.Add);
            }
        }

        // Process goals
        if (data.PatientGoals.Any())
        {
            data.LatestGoal = data.PatientGoals.OrderByDescending(g => g.StatusDate).FirstOrDefault();
        }

        // Process treatment directives
        ProcessTreatmentDirectives(data.AllConsents, data);

        // Process observations
        ProcessObservations(data);

        // Build encounter models
        BuildEncounterModels(allEncounters, allProcedures, allQuestionnaireResponses, data);

        return data;
    }

    private async Task<Patient?> LoadPatientDataAsync(Patient currentPatient)
    {
        var keys = await _localStorage.KeysAsync();
        var patientKey = $"Patient-{currentPatient.Id}";
        var patientKeys = keys.Where(k => k.Contains(patientKey)).ToList();
        
        if (patientKeys.Any())
        {
            var json = await _localStorage.GetItemAsStringAsync(patientKeys.First());
            if (!string.IsNullOrEmpty(json))
            {
                try 
                { 
                    return _parser.Deserialize<Patient>(json); 
                } 
                catch { /* Fall through to return current patient */ }
            }
        }
        
        return currentPatient;
    }

    private async Task TryLoadResourceAsync<T>(string key, Action<T> onSuccess) where T : Resource
    {
        var json = await _localStorage.GetItemAsStringAsync(key);
        if (!string.IsNullOrEmpty(json))
        {
            try 
            {
                var resource = _parser.Deserialize<T>(json);
                if (resource != null) 
                    onSuccess(resource);
            } 
            catch { /* Ignore parse errors */ }
        }
    }

    private void OnGoalLoaded(Goal goal, List<Goal> patientGoals)
    {
        if (goal.Description?.Coding?.Any(c => _allowedGoalCodes.Contains(c.Code)) == true)
        {
            patientGoals.Add(goal);
        }
    }

    private void ProcessTreatmentDirectives(List<Consent> allConsents, AcpIntegratedData data)
    {
        data.Permits.Clear();
        data.Denials.Clear();
        data.Others.Clear();

        var activeConsents = allConsents.Where(c => c.Status == Consent.ConsentState.Active).ToList();
        var grouped = activeConsents
            .GroupBy(c => c.Provision?.Code?.FirstOrDefault()?.Coding?.FirstOrDefault()?.Code ?? "Unknown")
            .ToList();

        foreach (var group in grouped)
        {
            var sorted = group.OrderByDescending(c => c.DateTime).ToList();
            if (!sorted.Any()) continue;
            
            var top = sorted.First();
            var vm = new TreatmentDirectiveViewModel
            {
                Consent = top,
                Title = top.Provision?.Code?.FirstOrDefault()?.Coding?.FirstOrDefault()?.Display 
                    ?? top.Provision?.Code?.FirstOrDefault()?.Text 
                    ?? "Onbekend",
                Date = !string.IsNullOrEmpty(top.DateTime) ? DateTime.Parse(top.DateTime) : null
            };

            if (top.Provision?.Type == Consent.ConsentProvisionType.Permit)
            {
                data.Permits.Add(vm);
            }
            else if (top.Provision?.Type == Consent.ConsentProvisionType.Deny)
            {
                data.Denials.Add(vm);
            }
            else
            {
                var ext = top.ModifierExtension.FirstOrDefault(e => e.Value is FhirString);
                if (ext != null)
                {
                    vm.SpecificationOther = (ext.Value as FhirString)?.Value;
                }
                data.Others.Add(vm);
            }
        }
    }

    private void ProcessObservations(AcpIntegratedData data)
    {
        data.LatestObservations.Clear();
        
        var relevant = data.AllObservations
            .Where(o => o.Code?.Coding?.Any(c => _allowedObservationCodes.Contains(c.Code)) == true)
            .ToList();

        var grouped = relevant.GroupBy(o => 
            o.Code.Coding.FirstOrDefault(c => _allowedObservationCodes.Contains(c.Code))?.Code ?? "Unknown");
        
        foreach (var group in grouped)
        {
            var latest = group.OrderByDescending(o => 
                (o.Effective as FhirDateTime)?.Value ?? o.Issued?.ToString("o") ?? ""
            ).FirstOrDefault();
            
            if (latest != null)
            {
                data.LatestObservations.Add(latest);
            }
        }
    }

    private void BuildEncounterModels(
        List<Encounter> allEncounters,
        List<Procedure> allProcedures,
        List<QuestionnaireResponse> allQuestionnaireResponses,
        AcpIntegratedData data)
    {
        var linkedQrIds = new HashSet<string>();


        // Deduplicate encounters
        var uniqueEncounters = ResourceDeduplicator.Deduplicate(allEncounters, e => e.Identifier);

        foreach (var enc in uniqueEncounters)
        {
            var linkedProcedures = allProcedures.Where(p =>
                (enc.ReasonReference.Any(r => MatchesRef(r, p))) ||
                (p.Encounter != null && MatchesRef(p.Encounter, enc))
            ).ToList();

            var acpProcedure = linkedProcedures.FirstOrDefault(p => IsAcpProcedure(p));

            if (acpProcedure != null || linkedProcedures.Any())
            {
                var linkedQRs = allQuestionnaireResponses
                    .Where(qr => qr.Encounter != null && MatchesRef(qr.Encounter, enc))
                    .ToList();
                linkedQrIds.UnionWith(linkedQRs.Select(q => q.Id).OfType<string>());

                var linkedObs = data.AllObservations
                    .Where(obs => obs.Encounter != null && MatchesRef(obs.Encounter, enc))
                    .ToList();

                var model = new AcpEncounterViewModel
                {
                    Encounter = enc,
                    Procedure = acpProcedure ?? linkedProcedures.FirstOrDefault(),
                    QuestionnaireResponses = linkedQRs,
                    Observations = linkedObs
                };

                // Parse date
                if (enc.Period?.Start != null && DateTime.TryParse(enc.Period.Start, out var d))
                {
                    model.Date = d;
                }
                else if (model.Procedure?.Performed is FhirDateTime fdt && DateTime.TryParse(fdt.Value, out var pd))
                {
                    model.Date = pd;
                }
                else
                {
                    model.Date = DateTime.MinValue;
                }

                // Parse participants
                model.Participants = GetParticipants(enc, model.Procedure);

                data.AcpEncounters.Add(model);
            }
        }

        // Identify unlinked questionnaires
        data.UnlinkedQuestionnaires = allQuestionnaireResponses
            .Where(qr => qr.Id != null && !linkedQrIds.Contains(qr.Id))
            .OrderByDescending(qr => qr.Authored)
            .ToList();
    }

    private bool IsAcpProcedure(Procedure? p)
    {
        if (p == null) return false;
        return p.Code?.Coding?.Any(c => c.Code == "713603004") == true;
    }

    private bool MatchesRef(ResourceReference reference, Resource target)
    {
        if (reference == null || target == null || string.IsNullOrEmpty(target.Id)) 
            return false;
        if (string.IsNullOrEmpty(reference.Reference)) 
            return false;
        
        return reference.Reference.EndsWith(target.Id) || reference.Reference.Contains($"/{target.Id}");
    }

    private List<ParticipantInfo> GetParticipants(Encounter enc, Procedure? proc)
    {
        var list = new List<ParticipantInfo>();

        if (enc?.Participant != null)
        {
            foreach (var p in enc.Participant)
            {
                if (p.Individual != null)
                {
                    bool isPractitioner = p.Individual.Reference?.Contains("Practitioner") == true;
                    list.Add(new ParticipantInfo(
                        p.Individual.Display ?? "Onbekend",
                        p.Individual.Reference ?? "",
                        isPractitioner,
                        null
                    ));
                }
            }
        }

        if (proc?.Performer != null)
        {
            foreach (var p in proc.Performer)
            {
                if (p.Actor != null && !list.Any(x => x.Reference == p.Actor.Reference))
                {
                    bool isPractitioner = p.Actor.Reference?.Contains("Practitioner") == true;
                    list.Add(new ParticipantInfo(
                        p.Actor.Display ?? "Onbekend",
                        p.Actor.Reference ?? "",
                        isPractitioner,
                        p.Function?.Text
                    ));
                }
            }
        }

        return list;
    }
}
