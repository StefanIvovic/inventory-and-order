using FluentValidation;

namespace BuildingBlocks.Validation;

public abstract class CommandValidator<T> : AbstractValidator<T>;
