namespace CinemaReservation.Api.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    // Centralising the supported roles prevents role-name inconsistencies
    // across authorization policies, seeding and user-management code.
    public static readonly string[] All =
    [
        Admin,
        User
    ];
}