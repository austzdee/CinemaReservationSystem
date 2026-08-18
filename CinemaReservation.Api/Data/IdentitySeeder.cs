using CinemaReservation.Api.Authorization;
using CinemaReservation.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CinemaReservation.Api.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        using var scope = services.CreateScope();

        var roleManager =
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        var userManager =
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        await SeedAdminAsync(userManager, configuration);
    }

    private static async Task SeedRolesAsync(
        RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in AppRoles.All)
        {
            // Role seeding is idempotent: restarting the application must not
            // create duplicate role records.
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(
                new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create role '{roleName}': {FormatErrors(result.Errors)}");
            }
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];

        // Initial administrator credentials must come from secure configuration,
        // never from source-controlled application code.
        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Initial administrator credentials are not configured.");
        }

        var admin = await userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult =
                await userManager.CreateAsync(admin, password);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create initial administrator: {FormatErrors(createResult.Errors)}");
            }
        }

        // Ensure an existing seeded administrator retains the required role
        // even if the role assignment was removed independently.
        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            var roleResult =
                await userManager.AddToRoleAsync(admin, AppRoles.Admin);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to assign Admin role: {FormatErrors(roleResult.Errors)}");
            }
        }
    }

    private static string FormatErrors(
        IEnumerable<IdentityError> errors)
    {
        return string.Join(
            "; ",
            errors.Select(error => error.Description));
    }
}