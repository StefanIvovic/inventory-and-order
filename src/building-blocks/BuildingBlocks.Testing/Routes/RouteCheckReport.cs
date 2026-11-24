using System.Text;

namespace BuildingBlocks.Testing;

public sealed record RouteCheckReport(IReadOnlyList<string> Checked, IReadOnlyList<string> Skipped, IReadOnlyList<string> Failures)
{
    public override string ToString()
    {
        var text = new StringBuilder();
        Append(text, "Checked", Checked);
        Append(text, "Skipped (allow-listed)", Skipped);
        Append(text, "Failures", Failures);
        return text.ToString();
    }

    private static void Append(StringBuilder text, string title, IReadOnlyList<string> lines)
    {
        text.Append(title).Append(" (").Append(lines.Count).AppendLine("):");
        foreach (var line in lines)
        {
            text.Append("  ").AppendLine(line);
        }
    }
}
