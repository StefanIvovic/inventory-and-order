# Contract: Validation API

```csharp
namespace BuildingBlocks.AspNetCore.Errors;
public sealed record MappedProblem(int Status, string? Code, string? Detail, IReadOnlyDictionary<string, object?> Extensions, LogLevel LogLevel, string? Title = null);
public interface IExceptionProblemMapper
{
    bool TryMap(Exception exception, HttpContext context, [NotNullWhen(true)] out MappedProblem? problem);
}

namespace BuildingBlocks.Validation;
public static class ValidationExtensions
{
    public static WolverineOptions UseBuildingBlocksValidation(this WolverineOptions options);
}
public static class FieldNames { public static string ToClientName(string propertyName); }
```

## Service usage

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.UseBuildingBlocksMessaging(settings);
    opts.UseBuildingBlocksValidation();
});

public sealed class TransferStockValidator : AbstractValidator<TransferStock>
{
    public TransferStockValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.ToWarehouseId).NotEmpty().NotEqual(x => x.FromWarehouseId);
    }
}
```

## 400 body

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "code": "validation-failed",
  "errors": {
    "quantity": ["'Quantity' must be greater than '0'."],
    "toWarehouseId": ["'To Warehouse Id' must not be empty.", "'To Warehouse Id' must not be equal to '…'."]
  },
  "traceId": "00-…"
}
```
