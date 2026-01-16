using Hl7.Fhir.Model;
using FauxHR.Modules.CrmiAuthoring.Services;

namespace FauxHR.Modules.CrmiAuthoring.Models;

/// <summary>
/// ViewModel for editing ActivityDefinition resources following CRMI profile.
/// </summary>
public class ActivityDefinitionViewModel
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
    public List<CodeableConceptViewModel> Topics { get; set; } = new();

    // Purpose and legal
    public string? Purpose { get; set; }
    public string? Copyright { get; set; }
    
    // CRMI Extensions
    public string? Usage { get; set; }
    public string? CopyrightLabel { get; set; }

    // Contributors
    public List<ContributorViewModel> Authors { get; set; } = new();
    public List<ContributorViewModel> Editors { get; set; } = new();
    public List<ContributorViewModel> Reviewers { get; set; } = new();
    public List<ContributorViewModel> Endorsers { get; set; } = new();

    // Related artifacts
    public List<RelatedArtifactViewModel> RelatedArtifacts { get; set; } = new();

    // Activity-specific content
    public ActivityDefinition.RequestResourceType? Kind { get; set; }
    public string? Profile { get; set; }
    public CodeableConceptViewModel? Code { get; set; }
    public string? Intent { get; set; }
    public string? Priority { get; set; }
    public bool DoNotPerform { get; set; }
    
    // Timing - supports different timing types
    public TimingViewModel Timing { get; set; } = new();
    
    // Participants
    public List<ParticipantViewModel> Participants { get; set; } = new();
    
    // Dynamic Values
    public List<DynamicValueViewModel> DynamicValues { get; set; } = new();

    /// <summary>
    /// Converts the ViewModel to an ActivityDefinition FHIR resource.
    /// </summary>
    public ActivityDefinition ToActivityDefinition()
    {
        var ad = new ActivityDefinition
        {
            Id = Id,
            Url = Url,
            Version = Version,
            Name = Name,
            Title = Title,
            Status = Status,
            Experimental = Experimental,
            DateElement = Date.HasValue ? new FhirDateTime(Date.Value) : null,
            Publisher = Publisher,
            Description = string.IsNullOrWhiteSpace(Description) ? null : new Markdown(Description),
            Purpose = string.IsNullOrWhiteSpace(Purpose) ? null : new Markdown(Purpose),
            Copyright = string.IsNullOrWhiteSpace(Copyright) ? null : new Markdown(Copyright),
            ApprovalDateElement = ApprovalDate.HasValue ? new Date(ApprovalDate.Value.ToString("yyyy-MM-dd")) : null,
            LastReviewDateElement = LastReviewDate.HasValue ? new Date(LastReviewDate.Value.ToString("yyyy-MM-dd")) : null,
            EffectivePeriod = EffectivePeriod.ToPeriod(),
            Kind = Kind,
            Profile = Profile,
            Code = Code?.ToCodeableConcept(),
            DoNotPerform = DoNotPerform,
            Timing = Timing.ToDataType()
        };

        // Intent
        if (!string.IsNullOrWhiteSpace(Intent))
        {
            ad.IntentElement = new Code<RequestIntent>(Enum.Parse<RequestIntent>(Intent, ignoreCase: true));
        }

        // Priority
        if (!string.IsNullOrWhiteSpace(Priority))
        {
            ad.PriorityElement = new Code<RequestPriority>(Enum.Parse<RequestPriority>(Priority, ignoreCase: true));
        }

        // Identifiers
        ad.Identifier = Identifiers.Select(i => i.ToIdentifier()).ToList();

        // Contacts
        ad.Contact = Contacts.Select(c => c.ToContactDetail()).ToList();

        // UseContext
        ad.UseContext = UseContexts.Select(u => u.ToUsageContext()).ToList();

        // Jurisdiction - filter out empty CodeableConcepts
        ad.Jurisdiction = Jurisdictions.Select(j => j.ToCodeableConcept()).Where(j => j != null).ToList()!;

        // Topics - filter out empty CodeableConcepts
        ad.Topic = Topics.Select(t => t.ToCodeableConcept()).Where(t => t != null).ToList()!;

        // Contributors
        ad.Author = Authors.Select(a => a.ToContactDetail()).ToList();
        ad.Editor = Editors.Select(e => e.ToContactDetail()).ToList();
        ad.Reviewer = Reviewers.Select(r => r.ToContactDetail()).ToList();
        ad.Endorser = Endorsers.Select(e => e.ToContactDetail()).ToList();

        // Related artifacts
        ad.RelatedArtifact = RelatedArtifacts.Select(r => r.ToRelatedArtifact()).ToList();

        // Participants
        ad.Participant = Participants.Where(p => p.HasContent).Select(p => p.ToParticipant()).ToList();

        // Dynamic Values
        ad.DynamicValue = DynamicValues.Where(d => d.HasContent).Select(d => d.ToDynamicValue()).ToList();

        // CRMI Extensions
        CrmiArtifactService.SetUsageExtension(ad, Usage);
        CrmiArtifactService.SetCopyrightLabelExtension(ad, CopyrightLabel);

        return ad;
    }

    /// <summary>
    /// Creates a ViewModel from an ActivityDefinition FHIR resource.
    /// </summary>
    public static ActivityDefinitionViewModel FromActivityDefinition(ActivityDefinition? ad)
    {
        if (ad == null) return new ActivityDefinitionViewModel();

        return new ActivityDefinitionViewModel
        {
            Id = ad.Id,
            Url = ad.Url,
            Version = ad.Version,
            Name = ad.Name,
            Title = ad.Title,
            Status = ad.Status ?? PublicationStatus.Draft,
            Experimental = ad.Experimental ?? false,
            Date = ad.DateElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime,
            Publisher = ad.Publisher,
            Description = ad.Description,
            Purpose = ad.Purpose,
            Copyright = ad.Copyright,
            ApprovalDate = ad.ApprovalDateElement != null ? DateTime.TryParse(ad.ApprovalDateElement.Value, out var appDate) ? appDate : null : null,
            LastReviewDate = ad.LastReviewDateElement != null ? DateTime.TryParse(ad.LastReviewDateElement.Value, out var revDate) ? revDate : null : null,
            EffectivePeriod = PeriodViewModel.FromPeriod(ad.EffectivePeriod),
            Kind = ad.Kind,
            Profile = ad.Profile,
            Code = CodeableConceptViewModel.FromCodeableConcept(ad.Code),
            Intent = ad.Intent?.ToString(),
            Priority = ad.Priority?.ToString(),
            DoNotPerform = ad.DoNotPerform ?? false,
            Timing = TimingViewModel.FromDataType(ad.Timing),

            // Collections
            Identifiers = ad.Identifier?.Select(x => IdentifierViewModel.FromIdentifier(x)).ToList() ?? new(),
            Contacts = ad.Contact?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            UseContexts = ad.UseContext?.Select(x => UsageContextViewModel.FromUsageContext(x)).ToList() ?? new(),
            Jurisdictions = ad.Jurisdiction?.Select(x => CodeableConceptViewModel.FromCodeableConcept(x)).ToList() ?? new(),
            Topics = ad.Topic?.Select(x => CodeableConceptViewModel.FromCodeableConcept(x)).ToList() ?? new(),
            Authors = ad.Author?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            Editors = ad.Editor?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            Reviewers = ad.Reviewer?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            Endorsers = ad.Endorser?.Select(x => ContributorViewModel.FromContactDetail(x)).ToList() ?? new(),
            RelatedArtifacts = ad.RelatedArtifact?.Select(x => RelatedArtifactViewModel.FromRelatedArtifact(x)).ToList() ?? new(),
            Participants = ad.Participant?.Select(x => ParticipantViewModel.FromParticipant(x)).ToList() ?? new(),
            DynamicValues = ad.DynamicValue?.Select(x => DynamicValueViewModel.FromDynamicValue(x)).ToList() ?? new(),

            // CRMI Extensions
            Usage = CrmiArtifactService.GetUsageExtension(ad),
            CopyrightLabel = CrmiArtifactService.GetCopyrightLabelExtension(ad)
        };
    }
}

/// <summary>
/// ViewModel for Identifier.
/// </summary>
public class IdentifierViewModel
{
    public string? System { get; set; }
    public string? Value { get; set; }
    public Identifier.IdentifierUse? Use { get; set; }

    public Identifier ToIdentifier()
    {
        return new Identifier
        {
            System = System,
            Value = Value,
            Use = Use
        };
    }

    public static IdentifierViewModel FromIdentifier(Identifier? id)
    {
        if (id == null) return new IdentifierViewModel();

        return new IdentifierViewModel
        {
            System = id.System,
            Value = id.Value,
            Use = id.Use
        };
    }
}

/// <summary>
/// ViewModel for CodeableConcept.
/// </summary>
public class CodeableConceptViewModel
{
    public string? Text { get; set; }
    public List<CodingViewModel> Codings { get; set; } = new();

    /// <summary>
    /// Returns true if this CodeableConcept has meaningful content.
    /// </summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(Text) || Codings.Any(c => c.HasContent);

    /// <summary>
    /// Converts to FHIR CodeableConcept, returning null if empty to avoid FHIR validation errors.
    /// </summary>
    public CodeableConcept? ToCodeableConcept()
    {
        // Filter out empty codings
        var validCodings = Codings.Select(c => c.ToCoding()).Where(c => c != null).ToList();
        
        // Return null if no content to avoid FHIR ele-1 constraint violation
        if (string.IsNullOrWhiteSpace(Text) && !validCodings.Any())
        {
            return null;
        }

        return new CodeableConcept
        {
            Text = string.IsNullOrWhiteSpace(Text) ? null : Text,
            Coding = validCodings!
        };
    }

    public static CodeableConceptViewModel FromCodeableConcept(CodeableConcept? cc)
    {
        if (cc == null) return new CodeableConceptViewModel();

        return new CodeableConceptViewModel
        {
            Text = cc.Text,
            Codings = cc.Coding?.Select(CodingViewModel.FromCoding).ToList() ?? new()
        };
    }
}

/// <summary>
/// ViewModel for Coding.
/// </summary>
public class CodingViewModel
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
    public string? Version { get; set; }

    /// <summary>
    /// Returns true if this Coding has meaningful content.
    /// </summary>
    public bool HasContent => !string.IsNullOrWhiteSpace(System) || 
                              !string.IsNullOrWhiteSpace(Code) || 
                              !string.IsNullOrWhiteSpace(Display);

    /// <summary>
    /// Converts to FHIR Coding, returning null if empty to avoid FHIR validation errors.
    /// </summary>
    public Coding? ToCoding()
    {
        // Return null if no meaningful content to avoid FHIR ele-1 constraint violation
        if (!HasContent)
        {
            return null;
        }

        return new Coding
        {
            System = string.IsNullOrWhiteSpace(System) ? null : System,
            Code = string.IsNullOrWhiteSpace(Code) ? null : Code,
            Display = string.IsNullOrWhiteSpace(Display) ? null : Display,
            Version = string.IsNullOrWhiteSpace(Version) ? null : Version
        };
    }

    public static CodingViewModel FromCoding(Coding? c)
    {
        if (c == null) return new CodingViewModel();

        return new CodingViewModel
        {
            System = c.System,
            Code = c.Code,
            Display = c.Display,
            Version = c.Version
        };
    }
}

/// <summary>
/// ViewModel for ActivityDefinition.timing[x] which can be DateTime, Period, Duration, or Timing.
/// </summary>
public class TimingViewModel
{
    public string TimingType { get; set; } = "DateTime"; // DateTime, Period, Duration, Timing
    
    // DateTime value
    public DateTime? DateTimeValue { get; set; }
    
    // Period values
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    
    // Duration values
    public decimal? DurationValue { get; set; }
    public string? DurationUnit { get; set; }
    
    // Timing values
    public int? TimingFrequency { get; set; }
    public decimal? TimingPeriod { get; set; }
    public string? TimingPeriodUnit { get; set; }
    
    // Additional Timing.repeat fields
    public int? TimingCount { get; set; }
    public int? TimingCountMax { get; set; }
    public decimal? TimingDuration { get; set; }
    public decimal? TimingDurationMax { get; set; }
    public string? TimingDurationUnit { get; set; }

    public bool HasContent => TimingType switch
    {
        "DateTime" => DateTimeValue.HasValue,
        "Period" => PeriodStart.HasValue || PeriodEnd.HasValue,
        "Duration" => DurationValue.HasValue,
        "Timing" => TimingFrequency.HasValue || TimingPeriod.HasValue || TimingCount.HasValue || TimingDuration.HasValue,
        _ => false
    };

    public DataType? ToDataType()
    {
        if (!HasContent) return null;

        return TimingType switch
        {
            "DateTime" when DateTimeValue.HasValue => new FhirDateTime(DateTimeValue.Value),
            "Period" => new Period
            {
                StartElement = PeriodStart.HasValue ? new FhirDateTime(PeriodStart.Value) : null,
                EndElement = PeriodEnd.HasValue ? new FhirDateTime(PeriodEnd.Value) : null
            },
            "Duration" when DurationValue.HasValue => new Duration
            {
                Value = DurationValue,
                Unit = DurationUnit,
                System = "http://unitsofmeasure.org",
                Code = DurationUnit
            },
            "Timing" => new Timing
            {
                Repeat = new Timing.RepeatComponent
                {
                    Frequency = TimingFrequency,
                    Period = TimingPeriod,
                    PeriodUnit = ParsePeriodUnit(TimingPeriodUnit),
                    Count = TimingCount,
                    CountMax = TimingCountMax,
                    Duration = TimingDuration,
                    DurationMax = TimingDurationMax,
                    DurationUnit = ParsePeriodUnit(TimingDurationUnit)
                }
            },
            _ => null
        };
    }

    private static Timing.UnitsOfTime? ParsePeriodUnit(string? unit)
    {
        if (string.IsNullOrEmpty(unit)) return null;
        return unit.ToLowerInvariant() switch
        {
            "s" or "second" => Timing.UnitsOfTime.S,
            "min" or "minute" => Timing.UnitsOfTime.Min,
            "h" or "hour" => Timing.UnitsOfTime.H,
            "d" or "day" => Timing.UnitsOfTime.D,
            "wk" or "week" => Timing.UnitsOfTime.Wk,
            "mo" or "month" => Timing.UnitsOfTime.Mo,
            "a" or "year" => Timing.UnitsOfTime.A,
            _ => null
        };
    }

    public static TimingViewModel FromDataType(DataType? timing)
    {
        if (timing == null) return new TimingViewModel();

        return timing switch
        {
            FhirDateTime dt => new TimingViewModel
            {
                TimingType = "DateTime",
                DateTimeValue = dt.ToDateTimeOffset(TimeSpan.Zero).DateTime
            },
            Period p => new TimingViewModel
            {
                TimingType = "Period",
                PeriodStart = p.StartElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime,
                PeriodEnd = p.EndElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime
            },
            Duration d => new TimingViewModel
            {
                TimingType = "Duration",
                DurationValue = d.Value,
                DurationUnit = d.Unit
            },
            Timing t => new TimingViewModel
            {
                TimingType = "Timing",
                TimingFrequency = t.Repeat?.Frequency,
                TimingPeriod = t.Repeat?.Period,
                TimingPeriodUnit = t.Repeat?.PeriodUnit?.ToString(),
                TimingCount = t.Repeat?.Count,
                TimingCountMax = t.Repeat?.CountMax,
                TimingDuration = t.Repeat?.Duration,
                TimingDurationMax = t.Repeat?.DurationMax,
                TimingDurationUnit = t.Repeat?.DurationUnit?.ToString()
            },
            _ => new TimingViewModel()
        };
    }
}

/// <summary>
/// ViewModel for ActivityDefinition.participant.
/// </summary>
public class ParticipantViewModel
{
    public string? Type { get; set; } // patient | practitioner | related-person | device
    public CodeableConceptViewModel Role { get; set; } = new();

    public bool HasContent => !string.IsNullOrWhiteSpace(Type);

    public ActivityDefinition.ParticipantComponent ToParticipant()
    {
        var participant = new ActivityDefinition.ParticipantComponent();

        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<ActionParticipantType>(Type, true, out var parsedType))
        {
            participant.Type = parsedType;
        }

        var role = Role.ToCodeableConcept();
        if (role != null)
        {
            participant.Role = role;
        }

        return participant;
    }

    public static ParticipantViewModel FromParticipant(ActivityDefinition.ParticipantComponent? p)
    {
        if (p == null) return new ParticipantViewModel();

        return new ParticipantViewModel
        {
            Type = p.Type?.ToString(),
            Role = CodeableConceptViewModel.FromCodeableConcept(p.Role)
        };
    }
}

/// <summary>
/// ViewModel for ActivityDefinition.dynamicValue.
/// </summary>
public class DynamicValueViewModel
{
    public string? Path { get; set; }
    public string? ExpressionLanguage { get; set; } = "text/fhirpath";
    public string? ExpressionValue { get; set; }
    public string? ExpressionDescription { get; set; }

    public bool HasContent => !string.IsNullOrWhiteSpace(Path) || !string.IsNullOrWhiteSpace(ExpressionValue);

    public ActivityDefinition.DynamicValueComponent ToDynamicValue()
    {
        return new ActivityDefinition.DynamicValueComponent
        {
            Path = Path,
            Expression = new Expression
            {
                Language = ExpressionLanguage,
                Expression_ = ExpressionValue,
                Description = ExpressionDescription
            }
        };
    }

    public static DynamicValueViewModel FromDynamicValue(ActivityDefinition.DynamicValueComponent? dv)
    {
        if (dv == null) return new DynamicValueViewModel();

        return new DynamicValueViewModel
        {
            Path = dv.Path,
            ExpressionLanguage = dv.Expression?.Language,
            ExpressionValue = dv.Expression?.Expression_,
            ExpressionDescription = dv.Expression?.Description
        };
    }
}
