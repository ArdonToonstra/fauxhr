using Hl7.Fhir.Model;
using System;

// Check ActivityDefinition nested types
foreach (var t in typeof(ActivityDefinition).GetNestedTypes())
    if (t.IsEnum) Console.WriteLine("ActivityDefinition." + t.Name);

// Check ChargeItemDefinition nested types  
foreach (var t in typeof(ChargeItemDefinition).GetNestedTypes())
    if (t.IsEnum) Console.WriteLine("ChargeItemDefinition." + t.Name);
