using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Errors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Lookups;

public static class LookupReader
{
    public const string TypeNotFoundCode = "lookup-type-not-found";
    private const string Escape = "\\";

    [SuppressMessage("Globalization", "CA1304", Justification = "Translated to SQL lower().")]
    [SuppressMessage("Globalization", "CA1311", Justification = "Translated to SQL lower().")]
    [SuppressMessage("Globalization", "CA1309", Justification = "Translated to a SQL comparison in the column's collation.")]
    public static async Task<LookupValuePage> ReadValuesAsync(this DbContext db, LookupValueQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(query);
        await LookupValueQueryValidator.Instance.ValidateAndThrowAsync(query, cancellationToken);
        LookupCursor.TryDecode(query.After, out var after);

        var typeCode = query.TypeCode;
        var values = db.Set<LookupValue>().AsNoTracking()
            .Where(v => v.LookupTypeId == db.Set<LookupType>().Where(t => t.Code == typeCode).Select(t => t.Id).FirstOrDefault());

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var pattern = "%" + EscapeLike(query.Q.Trim().ToLowerInvariant()) + "%";
            values = values.Where(v => EF.Functions.Like(v.Name.ToLower(), pattern, Escape) || EF.Functions.Like(v.Code, pattern, Escape));
        }

        if (query.Ids is { Count: > 0 } ids)
        {
            values = values.Where(v => ids.Contains(v.Id));
        }

        if (query.Codes is { Count: > 0 } codes)
        {
            values = values.Where(v => codes.Contains(v.Code));
        }

        if (after is not null)
        {
            var name = after.Name;
            var id = after.Id;
            values = values.Where(v => string.Compare(v.Name, name) >= 0 && (string.Compare(v.Name, name) > 0 || v.Id.CompareTo(id) > 0));
        }

        var rows = await values
            .OrderBy(v => v.Name)
            .ThenBy(v => v.Id)
            .Take(query.Size + 1)
            .Select(v => new LookupValueItem(v.Id, v.Code, v.Name))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0 && !await db.Set<LookupType>().AnyAsync(t => t.Code == typeCode, cancellationToken))
        {
            throw new NotFoundException("lookup type", TypeNotFoundCode);
        }

        var hasMore = rows.Count > query.Size;
        var items = hasMore ? rows[..query.Size] : rows;
        var next = hasMore ? new LookupCursor(items[^1].Name, items[^1].Id).Encode() : null;
        return new LookupValuePage(items, next);
    }

    private static string EscapeLike(string text) =>
        text.Replace(Escape, Escape + Escape, StringComparison.Ordinal)
            .Replace("%", Escape + "%", StringComparison.Ordinal)
            .Replace("_", Escape + "_", StringComparison.Ordinal);
}
