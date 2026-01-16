using Hl7.Fhir.Model;
using FauxHR.Modules.CrmiAuthoring.Services;

namespace FauxHR.Modules.CrmiAuthoring.Models;

/// <summary>
/// ViewModel for editing ChargeItemDefinition resources following CRMI shareable profile.
/// </summary>
public class ChargeItemDefinitionViewModel
{
    // Resource identity
    public string? Id { get; set; }
    public string? Url { get; set; }
    public string? Version { get; set; } = "1.0.0";
    public string? Name { get; set; }
    public string? Title { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.Draft;
    public bool Experimental { get; set; }

    // Identifiers
    public List<IdentifierViewModel> Identifiers { get; set; } = new();

    // Dates
    public DateTime? Date { get; set; } = DateTime.Now;
    public DateTime? ApprovalDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public PeriodViewModel EffectivePeriod { get; set; } = new();

    // Publisher info
    public string? Publisher { get; set; }
    public List<ContributorViewModel> Contacts { get; set; } = new();
    public string? Description { get; set; }

    // Context
    public List<UsageContextViewModel> UseContexts { get; set; } = new();
    public List<CodeableConceptViewModel> Jurisdictions { get; set; } = new();

    // Purpose and legal
    public string? Copyright { get; set; }
    
    // CRMI Extensions
    public string? CopyrightLabel { get; set; }

    // ChargeItemDefinition-specific content
    public List<string> DerivedFromUris { get; set; } = new();
    public List<string> PartOf { get; set; } = new();
    public List<string> Replaces { get; set; } = new();
    
    // Billing code this definition applies to
    public CodeableConceptViewModel? Code { get; set; }
    
    // Instances this definition applies to
    public List<string> Instances { get; set; } = new();
    
    // Applicability rules
    public List<ApplicabilityViewModel> Applicabilities { get; set; } = new();
    
    // Property groups with price components
    public List<PropertyGroupViewModel> PropertyGroups { get; set; } = new();

    /// <summary>
    /// Converts the ViewModel to a ChargeItemDefinition FHIR resource.
    /// </summary>
    public ChargeItemDefinition ToChargeItemDefinition()
    {
        var cid = new ChargeItemDefinition
        {
            Id = Id,
            Url = Url,
            Version = Version,
            Title = Title,
            Status = Status,
            Experimental = Experimental,
            DateElement = Date.HasValue ? new FhirDateTime(Date.Value) : null,
            Publisher = Publisher,
            Description = string.IsNullOrWhiteSpace(Description) ? null : new Markdown(Description),
            Copyright = string.IsNullOrWhiteSpace(Copyright) ? null : new Markdown(Copyright),
            ApprovalDateElement = ApprovalDate.HasValue ? new Date(ApprovalDate.Value.ToString("yyyy-MM-dd")) : null,
            LastReviewDateElement = LastReviewDate.HasValue ? new Date(LastReviewDate.Value.ToString("yyyy-MM-dd")) : null,
            EffectivePeriod = EffectivePeriod.ToPeriod(),
            Code = Code?.ToCodeableConcept()
        };

        // Identifiers
        cid.Identifier = Identifiers.Select(i => i.ToIdentifier()).ToList();

        // Contacts
        cid.Contact = Contacts.Select(c => c.ToContactDetail()).ToList();

        // UseContext
        cid.UseContext = UseContexts.Select(u => u.ToUsageContext()).ToList();

        // Jurisdiction - filter out empty CodeableConcepts
        cid.Jurisdiction = Jurisdictions.Select(j => j.ToCodeableConcept()).Where(j => j != null).ToList()!;

        // URIs
        cid.DerivedFromUriElement = DerivedFromUris.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => new FhirUri(u)).ToList();
        cid.PartOfElement = PartOf.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => new Canonical(u)).ToList();
        cid.ReplacesElement = Replaces.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => new Canonical(u)).ToList();
        
        // Instances
        cid.Instance = Instances.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => new ResourceReference(i)).ToList();

        // Applicability
        cid.Applicability = Applicabilities.Select(a => a.ToApplicability()).ToList();

        // Property groups
        cid.PropertyGroup = PropertyGroups.Select(pg => pg.ToPropertyGroup()).ToList();

        // CRMI Extensions
        CrmiArtifactService.SetCopyrightLabelExtension(cid, CopyrightLabel);

        return cid;
    }

    /// <summary>
    /// Creates a ViewModel from a ChargeItemDefinition FHIR resource.
    /// </summary>
    public static ChargeItemDefinitionViewModel FromChargeItemDefinition(ChargeItemDefinition? cid)
    {
        if (cid == null) return new ChargeItemDefinitionViewModel();

        return new ChargeItemDefinitionViewModel
        {
            Id = cid.Id,
            Url = cid.Url,
            Version = cid.Version,
            Title = cid.Title,
            Status = cid.Status ?? PublicationStatus.Draft,
            Experimental = cid.Experimental ?? false,
            Date = cid.DateElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime,
            Publisher = cid.Publisher,
            Description = cid.Description,
            Copyright = cid.Copyright,
            ApprovalDate = cid.ApprovalDateElement != null ? DateTime.TryParse(cid.ApprovalDateElement.Value, out var appDate) ? appDate : null : null,
            LastReviewDate = cid.LastReviewDateElement != null ? DateTime.TryParse(cid.LastReviewDateElement.Value, out var revDate) ? revDate : null : null,
            EffectivePeriod = PeriodViewModel.FromPeriod(cid.EffectivePeriod),
            Code = CodeableConceptViewModel.FromCodeableConcept(cid.Code),

            // Collections
            Identifiers = cid.Identifier?.Select(x => IdentifierViewModel.FromIdentifier(x)).ToList() ?? new(),
            Contacts = cid.Contact?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            UseContexts = cid.UseContext?.Select(x => UsageContextViewModel.FromUsageContext(x)).ToList() ?? new(),
            Jurisdictions = cid.Jurisdiction?.Select(x => CodeableConceptViewModel.FromCodeableConcept(x)).ToList() ?? new(),
            
            // URIs
            DerivedFromUris = cid.DerivedFromUriElement?.Select(u => u.Value).Where(v => v != null).ToList() ?? new(),
            PartOf = cid.PartOfElement?.Select(c => c.Value).Where(v => v != null).ToList() ?? new(),
            Replaces = cid.ReplacesElement?.Select(c => c.Value).Where(v => v != null).ToList() ?? new(),
            Instances = cid.Instance?.Select(r => r.Reference).Where(r => r != null).ToList() ?? new(),
            
            // Applicability
            Applicabilities = cid.Applicability?.Select(x => ApplicabilityViewModel.FromApplicability(x)).ToList() ?? new(),
            
            // Property groups
            PropertyGroups = cid.PropertyGroup?.Select(x => PropertyGroupViewModel.FromPropertyGroup(x)).ToList() ?? new(),

            // CRMI Extensions
            CopyrightLabel = CrmiArtifactService.GetCopyrightLabelExtension(cid)
        };
    }
}

/// <summary>
/// ViewModel for ChargeItemDefinition.Applicability.
/// </summary>
public class ApplicabilityViewModel
{
    public string? Description { get; set; }
    public string? Language { get; set; }
    public string? Expression { get; set; }

    public ChargeItemDefinition.ApplicabilityComponent ToApplicability()
    {
        return new ChargeItemDefinition.ApplicabilityComponent
        {
            Description = Description,
            Language = Language,
            Expression = Expression
        };
    }

    public static ApplicabilityViewModel FromApplicability(ChargeItemDefinition.ApplicabilityComponent? a)
    {
        if (a == null) return new ApplicabilityViewModel();

        return new ApplicabilityViewModel
        {
            Description = a.Description,
            Language = a.Language,
            Expression = a.Expression
        };
    }
}

/// <summary>
/// ViewModel for ChargeItemDefinition.PropertyGroup.
/// </summary>
public class PropertyGroupViewModel
{
    public List<ApplicabilityViewModel> Applicabilities { get; set; } = new();
    public List<PriceComponentViewModel> PriceComponents { get; set; } = new();

    public ChargeItemDefinition.PropertyGroupComponent ToPropertyGroup()
    {
        return new ChargeItemDefinition.PropertyGroupComponent
        {
            Applicability = Applicabilities.Select(a => a.ToApplicability()).ToList(),
            PriceComponent = PriceComponents.Select(pc => pc.ToPriceComponent()).ToList()
        };
    }

    public static PropertyGroupViewModel FromPropertyGroup(ChargeItemDefinition.PropertyGroupComponent? pg)
    {
        if (pg == null) return new PropertyGroupViewModel();

        return new PropertyGroupViewModel
        {
            Applicabilities = pg.Applicability?.Select(ApplicabilityViewModel.FromApplicability).ToList() ?? new(),
            PriceComponents = pg.PriceComponent?.Select(PriceComponentViewModel.FromPriceComponent).ToList() ?? new()
        };
    }
}

/// <summary>
/// ViewModel for ChargeItemDefinition.PropertyGroup.PriceComponent.
/// </summary>
public class PriceComponentViewModel
{
    public InvoicePriceComponentType? Type { get; set; }
    public CodeableConceptViewModel? Code { get; set; }
    public decimal? Factor { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    public ChargeItemDefinition.PriceComponentComponent ToPriceComponent()
    {
        var pc = new ChargeItemDefinition.PriceComponentComponent
        {
            Type = Type,
            Code = Code?.ToCodeableConcept(),
            Factor = Factor
        };

        if (Amount.HasValue)
        {
            pc.Amount = new Money
            {
                Value = Amount,
                Currency = string.IsNullOrEmpty(Currency) ? Money.Currencies.EUR : Enum.Parse<Money.Currencies>(Currency!)
            };
        }

        return pc;
    }

    public static PriceComponentViewModel FromPriceComponent(ChargeItemDefinition.PriceComponentComponent? pc)
    {
        if (pc == null) return new PriceComponentViewModel();

        return new PriceComponentViewModel
        {
            Type = pc.Type,
            Code = CodeableConceptViewModel.FromCodeableConcept(pc.Code),
            Factor = pc.Factor,
            Amount = pc.Amount?.Value,
            Currency = pc.Amount?.Currency?.ToString()
        };
    }
}
