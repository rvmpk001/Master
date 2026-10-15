namespace Master.WinUI.Data;

internal sealed record AuthenticatedUser(
    int Id,
    string Username,
    string FullName,
    string Role);
