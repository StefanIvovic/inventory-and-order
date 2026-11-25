using IdentityDbContext = Identity.Api.Persistence.IdentityDbContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Api.Users;

public sealed class HandlerUserManager(
    IdentityDbContext db,
    IOptions<IdentityOptions> options,
    IPasswordHasher<AppUser> passwordHasher,
    IEnumerable<IUserValidator<AppUser>> userValidators,
    IEnumerable<IPasswordValidator<AppUser>> passwordValidators,
    ILookupNormalizer normalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    ILogger<UserManager<AppUser>> logger)
    : UserManager<AppUser>(
        new UserOnlyStore<AppUser, IdentityDbContext, Guid>(db, errors) { AutoSaveChanges = false },
        options,
        passwordHasher,
        userValidators,
        passwordValidators,
        normalizer,
        errors,
        services,
        logger);
