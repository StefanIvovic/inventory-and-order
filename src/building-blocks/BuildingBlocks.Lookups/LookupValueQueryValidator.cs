using FluentValidation;

namespace BuildingBlocks.Lookups;

internal sealed class LookupValueQueryValidator : AbstractValidator<LookupValueQuery>
{
    public static readonly LookupValueQueryValidator Instance = new();

    public LookupValueQueryValidator()
    {
        RuleFor(q => q.TypeCode).NotEmpty().MaximumLength(LookupType.CodeMaxLength);
        RuleFor(q => q.Q).MaximumLength(LookupValueQuery.MaxSearchLength);
        RuleFor(q => q.Size).InclusiveBetween(1, LookupValueQuery.MaxSize);
        RuleFor(q => q.Ids).Must(ids => ids is null || ids.Count <= LookupValueQuery.MaxListed)
            .WithMessage($"At most {LookupValueQuery.MaxListed} ids can be requested.");
        RuleFor(q => q.Codes).Must(codes => codes is null || codes.Count <= LookupValueQuery.MaxListed)
            .WithMessage($"At most {LookupValueQuery.MaxListed} codes can be requested.");
        RuleFor(q => q.After).Must(after => LookupCursor.TryDecode(after, out _))
            .WithMessage("The page token is invalid.");
    }
}
