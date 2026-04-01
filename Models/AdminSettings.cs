namespace ReplayFilesViewApi.Models;

public class AdminSettings
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
}
