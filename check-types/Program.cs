using System;
using Hl7.Fhir.Model;
var prop = typeof(RelatedArtifact).GetProperty("Url");
Console.WriteLine("RelatedArtifact.Url type: " + prop.PropertyType.FullName);
