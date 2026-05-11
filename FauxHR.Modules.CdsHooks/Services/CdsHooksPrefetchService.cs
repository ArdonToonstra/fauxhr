using FauxHR.Core.Interfaces;
using FauxHR.Modules.CdsHooks.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FauxHR.Modules.CdsHooks.Services;

/// <summary>
/// Gathers the USCDI+ CTM V1 prefetch data elements from the FHIR server
/// and assembles a CDS Hooks patient-view request.
/// </summary>
public class CdsHooksPrefetchService
{
    private readonly IFhirService _fhirService;

    public CdsHooksPrefetchService(IFhirService fhirService)
    {
        _fhirService = fhirService;
    }

    /// <summary>
    /// Gathers all CTM-relevant prefetch data for a patient and returns a populated CdsHooksRequest.
    /// </summary>
    public async Task<CdsHooksRequest> BuildPatientViewRequestAsync(string patientId, string userId)
    {
        var request = new CdsHooksRequest
        {
            Hook = "patient-view",
            HookInstance = Guid.NewGuid().ToString(),
            Context = new CdsContext
            {
                PatientId = patientId,
                UserId = userId
            }
        };

        // Execute prefetch queries in parallel
        var patientTask = _fhirService.GetPatientByIdAsync(patientId);
        var conditionsProblemListTask = _fhirService.SearchResourceAsync("Condition",
            $"patient={patientId}&category=http://terminology.hl7.org/CodeSystem/condition-category|problem-list-item");
        var conditionsSitesTask = _fhirService.SearchResourceAsync("Condition",
            $"patient={patientId}");
        var observationsPerformanceTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://loinc.org|89247-1,http://loinc.org|89243-0,http://loinc.org|89246-3");
        var observationsSmokingTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://loinc.org|72166-2");
        var observationsLabTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&category=http://terminology.hl7.org/CodeSystem/observation-category|laboratory");
        var observationsPregnancyTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://loinc.org|82810-3");
        var observationsComorbiditiesTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://snomed.info/sct|398192003");
        var observationsStageTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://loinc.org|21908-9,http://loinc.org|21902-2,http://loinc.org|21914-7," +
            $"http://loinc.org|21905-5,http://loinc.org|21899-0,http://loinc.org|21911-3," +
            $"http://loinc.org|21906-3,http://loinc.org|21900-6,http://loinc.org|21912-1," +
            $"http://loinc.org|21907-1,http://loinc.org|21901-4,http://loinc.org|21913-9");
        var observationsHistologyTask = _fhirService.SearchResourceAsync("Observation",
            $"patient={patientId}&code=http://loinc.org|31206-6");
        var medicationAdminTask = _fhirService.SearchResourceAsync("MedicationAdministration",
            $"patient={patientId}&_include=MedicationAdministration:medication");
        var proceduresTask = _fhirService.SearchResourceAsync("Procedure",
            $"patient={patientId}");
        var proceduresRadiotherapyTask = _fhirService.SearchResourceAsync("Procedure",
            $"patient={patientId}&code=http://snomed.info/sct|1217123003");

        await System.Threading.Tasks.Task.WhenAll(
            patientTask, conditionsProblemListTask, conditionsSitesTask,
            observationsPerformanceTask, observationsSmokingTask, observationsLabTask,
            observationsPregnancyTask, observationsComorbiditiesTask, observationsStageTask,
            observationsHistologyTask, medicationAdminTask, proceduresTask, proceduresRadiotherapyTask);

        // Assemble prefetch dictionary
        var patient = await patientTask;
        if (patient != null)
            request.Prefetch["patient"] = patient;

        request.Prefetch["conditions-problem-list"] = await conditionsProblemListTask;
        request.Prefetch["conditions-sites"] = await conditionsSitesTask;
        request.Prefetch["observations-performance"] = await observationsPerformanceTask;
        request.Prefetch["observations-smoking"] = await observationsSmokingTask;
        request.Prefetch["observations-laboratory"] = await observationsLabTask;
        request.Prefetch["observations-pregnancy-status"] = await observationsPregnancyTask;
        request.Prefetch["observations-comorbidities"] = await observationsComorbiditiesTask;
        request.Prefetch["observations-stage"] = await observationsStageTask;
        request.Prefetch["observations-histology"] = await observationsHistologyTask;
        request.Prefetch["medication-administrations"] = await medicationAdminTask;
        request.Prefetch["procedures"] = await proceduresTask;
        request.Prefetch["procedures-radiotherapy"] = await proceduresRadiotherapyTask;

        return request;
    }

    /// <summary>
    /// Serializes the CDS Hooks request to JSON for display/debugging.
    /// </summary>
    public string SerializeRequest(CdsHooksRequest request)
    {
        var serializer = new FhirJsonSerializer();

        var jsonPrefetch = new Dictionary<string, string>();
        foreach (var kvp in request.Prefetch)
        {
            if (kvp.Value is Resource resource)
                jsonPrefetch[kvp.Key] = serializer.SerializeToString(resource);
            else
                jsonPrefetch[kvp.Key] = "null";
        }

        // Build a simplified JSON representation
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            hook = request.Hook,
            hookInstance = request.HookInstance,
            context = new
            {
                userId = request.Context.UserId,
                patientId = request.Context.PatientId,
                encounterId = request.Context.EncounterId
            },
            fhirServer = request.FhirServer,
            prefetch = jsonPrefetch
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

        return json;
    }
}
