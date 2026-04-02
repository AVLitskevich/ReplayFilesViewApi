using System.Security.Cryptography;
using System.Text;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface IAuthService
{
    bool VerifyPassword(string inputPassword, AdminSettings admin);
    (string hash, string salt) GenerateHash(string password);
}

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private const int Iterations = 1;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public AuthService(ILogger<AuthService> logger)
    {
        _logger = logger;
    }

    public bool VerifyPassword(string inputPassword, AdminSettings admin)
    {
        if (string.IsNullOrEmpty(inputPassword) || admin == null)
            return false;

        var salt = Convert.FromBase64String(admin.Salt);
        var expectedHash = Convert.FromBase64String(admin.PasswordHash);

        using var pbkdf2 = new Rfc2898DeriveBytes(inputPassword, salt, Iterations, HashAlgorithmName.SHA256);
        var actualHash = pbkdf2.GetBytes(HashSize);

        bool result = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        
        _logger.LogInformation("Auth Check - Input: {Input}, StoredHash: {Stored}, ComputedHash: {Computed}, Result: {Result}", 
            inputPassword, admin.PasswordHash, Convert.ToBase64String(actualHash), result);

        return result;
    }

    public (string hash, string salt) GenerateHash(string password)
    {
        var salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }
}
