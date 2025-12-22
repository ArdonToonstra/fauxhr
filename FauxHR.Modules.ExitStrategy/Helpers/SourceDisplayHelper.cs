namespace FauxHR.Modules.ExitStrategy.Helpers;

public static class SourceDisplayHelper
{
    public static string GetSourceLabel(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return "Onbekend";

        // If it contains a pipe (e.g. urn:oid:1.2.3|SomeLabel), take the last part
        if (source.Contains('|'))
        {
            return source.Split('|').Last().Trim();
        }

        // If it's a URL, try to get the host
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        return source;
    }
}
