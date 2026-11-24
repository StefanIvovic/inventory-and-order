namespace BuildingBlocks.Validation;

public static class FieldNames
{
    public static string ToClientName(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        return string.Join('.', propertyName.Split('.').Select(CamelCase));
    }

    private static string CamelCase(string segment) =>
        segment.Length == 0 || char.IsLower(segment[0])
            ? segment
            : string.Concat(char.ToLowerInvariant(segment[0]).ToString(), segment[1..]);
}
