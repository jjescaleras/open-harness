namespace OpenHarness.Domain.Common;

/// <summary>Values stored in <c>User.Role</c> and emitted as the JWT <c>role</c> claim.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string User = "user";
}
