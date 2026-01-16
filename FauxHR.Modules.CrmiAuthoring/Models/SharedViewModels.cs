using Hl7.Fhir.Model;

namespace FauxHR.Modules.CrmiAuthoring.Models;

/// <summary>
/// ViewModel for ContactDetail (used for author, editor, reviewer, endorser, contact).
/// </summary>
public class ContributorViewModel
{
    public string? Name { get; set; }
    public List<ContactPointViewModel> Telecoms { get; set; } = new();

    public ContactDetail ToContactDetail()
    {
        return new ContactDetail
        {
            Name = Name,
            Telecom = Telecoms.Select(t => t.ToContactPoint()).ToList()
        };
    }

    public static ContributorViewModel FromContactDetail(ContactDetail? contact)
    {
        if (contact == null) return new ContributorViewModel();
        
        return new ContributorViewModel
        {
            Name = contact.Name,
            Telecoms = contact.Telecom?.Select(x => ContactPointViewModel.FromContactPoint(x)).ToList() ?? new()
        };
    }
}

/// <summary>
/// ViewModel for ContactPoint.
/// </summary>
public class ContactPointViewModel
{
    public ContactPoint.ContactPointSystem? System { get; set; }
    public string? Value { get; set; }
    public ContactPoint.ContactPointUse? Use { get; set; }

    public ContactPoint ToContactPoint()
    {
        return new ContactPoint
        {
            System = System,
            Value = Value,
            Use = Use
        };
    }

    public static ContactPointViewModel FromContactPoint(ContactPoint? cp)
    {
        if (cp == null) return new ContactPointViewModel();
        
        return new ContactPointViewModel
        {
            System = cp.System,
            Value = cp.Value,
            Use = cp.Use
        };
    }
}

/// <summary>
/// ViewModel for RelatedArtifact with CRMI extensions.
/// </summary>
public class RelatedArtifactViewModel
{
    public RelatedArtifact.RelatedArtifactType? Type { get; set; }
    public string? Label { get; set; }
    public string? Display { get; set; }
    public string? Citation { get; set; }
    public string? Url { get; set; }
    public string? Resource { get; set; }
    
    // CRMI Extensions
    public DateTime? PublicationDate { get; set; }
    public PublicationStatus? PublicationStatus { get; set; }

    public RelatedArtifact ToRelatedArtifact()
    {
        var ra = new RelatedArtifact
        {
            Type = Type,
            Label = Label,
            Display = Display,
            Citation = string.IsNullOrWhiteSpace(Citation) ? null : new Markdown(Citation),
            Url = Url,
            Resource = Resource
        };

        // Add CRMI extensions
        if (PublicationDate.HasValue)
        {
            ra.Extension.Add(new Extension(
                "http://hl7.org/fhir/StructureDefinition/cqf-publicationDate",
                new Date(PublicationDate.Value.ToString("yyyy-MM-dd"))));
        }

        if (PublicationStatus.HasValue)
        {
            ra.Extension.Add(new Extension(
                "http://hl7.org/fhir/StructureDefinition/cqf-publicationStatus",
                new Code(PublicationStatus.Value.ToString().ToLowerInvariant())));
        }

        return ra;
    }

    public static RelatedArtifactViewModel FromRelatedArtifact(RelatedArtifact? ra)
    {
        if (ra == null) return new RelatedArtifactViewModel();

        var vm = new RelatedArtifactViewModel
        {
            Type = ra.Type,
            Label = ra.Label,
            Display = ra.Display,
            Citation = ra.Citation,
            Url = ra.Url,
            Resource = ra.Resource
        };

        // Extract CRMI extensions
        var pubDateExt = ra.Extension?.FirstOrDefault(e => 
            e.Url == "http://hl7.org/fhir/StructureDefinition/cqf-publicationDate");
        if (pubDateExt?.Value is Date d && DateTime.TryParse(d.Value, out var pubDate))
        {
            vm.PublicationDate = pubDate;
        }

        var pubStatusExt = ra.Extension?.FirstOrDefault(e => 
            e.Url == "http://hl7.org/fhir/StructureDefinition/cqf-publicationStatus");
        if (pubStatusExt?.Value is Code c && Enum.TryParse<PublicationStatus>(c.Value, true, out var status))
        {
            vm.PublicationStatus = status;
        }

        return vm;
    }
}

/// <summary>
/// ViewModel for UsageContext.
/// </summary>
public class UsageContextViewModel
{
    public string? CodeSystem { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
    
    // Value can be CodeableConcept, Quantity, Range, or Reference
    public string? ValueType { get; set; } = "CodeableConcept";
    public string? ValueCodeSystem { get; set; }
    public string? ValueCode { get; set; }
    public string? ValueDisplay { get; set; }

    public UsageContext ToUsageContext()
    {
        var uc = new UsageContext
        {
            Code = new Coding(CodeSystem, Code, Display)
        };

        if (ValueType == "CodeableConcept")
        {
            uc.Value = new CodeableConcept(ValueCodeSystem, ValueCode, ValueDisplay, null);
        }

        return uc;
    }

    public static UsageContextViewModel FromUsageContext(UsageContext? uc)
    {
        if (uc == null) return new UsageContextViewModel();

        var vm = new UsageContextViewModel
        {
            CodeSystem = uc.Code?.System,
            Code = uc.Code?.Code,
            Display = uc.Code?.Display
        };

        if (uc.Value is CodeableConcept cc)
        {
            vm.ValueType = "CodeableConcept";
            var coding = cc.Coding?.FirstOrDefault();
            vm.ValueCodeSystem = coding?.System;
            vm.ValueCode = coding?.Code;
            vm.ValueDisplay = coding?.Display ?? cc.Text;
        }

        return vm;
    }
}

/// <summary>
/// ViewModel for Period.
/// </summary>
public class PeriodViewModel
{
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

    public Period? ToPeriod()
    {
        if (!Start.HasValue && !End.HasValue) return null;

        return new Period
        {
            StartElement = Start.HasValue ? new FhirDateTime(Start.Value) : null,
            EndElement = End.HasValue ? new FhirDateTime(End.Value) : null
        };
    }

    public static PeriodViewModel FromPeriod(Period? period)
    {
        if (period == null) return new PeriodViewModel();

        return new PeriodViewModel
        {
            Start = period.StartElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime,
            End = period.EndElement?.ToDateTimeOffset(TimeSpan.Zero).DateTime
        };
    }
}
