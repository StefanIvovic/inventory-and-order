namespace BuildingBlocks.Validation;

public interface ICommandValidation<in T>
{
    Task EnsureValidAsync(T command, CancellationToken cancellationToken);
}
