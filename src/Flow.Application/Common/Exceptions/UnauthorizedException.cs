namespace Flow.Application.Common.Exceptions;

/// <summary>
/// Authentication failed: the caller did not prove who they are.
///
/// Deliberately distinct from <see cref="ForbiddenException"/>, which means the caller is
/// known and simply not allowed. The distinction is not pedantry — the mobile client keys
/// its behaviour off it: 401 sends the user to sign in again, 403 tells them the action is
/// not theirs to take. Collapsing the two makes a wrong password look like a permission
/// problem, and an expired session look like a bug.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication failed.") : base(message) { }
}
