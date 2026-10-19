using System.Net.Mail;
using System.Security.Cryptography;
using GymTrack.Data;
using GymTrack.Models;
using SQLite;

namespace GymTrack.Services;

public sealed class AccountService : IAccountService, IAsyncDisposable
{
    private const int PasswordIterations = 600_000;
    private const int HashSize = 32;
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isInitialized;

    public AccountService(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SQLiteAsyncConnection(
            databasePath,
            DatabaseConstants.OpenFlags,
            storeDateTimeAsTicks: false);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            await _connection.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS Account (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Email TEXT NOT NULL COLLATE NOCASE UNIQUE,
                    Role INTEGER NOT NULL CHECK (Role IN (0, 1)),
                    IsActive INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
                    CreatedAt TEXT NOT NULL,
                    PasswordSalt TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    PasswordIterations INTEGER NOT NULL
                )
                """);
            await _connection.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS AccountMetadata (Key TEXT PRIMARY KEY, Value TEXT NOT NULL)");

            var seeded = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AccountMetadata WHERE Key = 'DemoAccountsSeeded'");

            if (seeded == 0)
            {
                var demoAccounts = await Task.Run(() => new[]
                {
                    CreateDemoAccount("Maria Silva", "consumidor@gymtrack.app", UserRole.Consumer),
                    CreateDemoAccount("Equipe GymTrack", "funcionario@gymtrack.app", UserRole.Employee)
                });

                await _connection.RunInTransactionAsync(connection =>
                {
                    if (connection.ExecuteScalar<int>(
                            "SELECT COUNT(*) FROM AccountMetadata WHERE Key = 'DemoAccountsSeeded'") != 0)
                    {
                        return;
                    }

                    foreach (var account in demoAccounts)
                    {
                        connection.Insert(account, "OR IGNORE");
                    }

                    connection.Execute("INSERT INTO AccountMetadata (Key, Value) VALUES ('DemoAccountsSeeded', '1')");
                });
            }

            _isInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<AppUser?> SignInAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await InitializeAsync();
        var account = await _connection.FindWithQueryAsync<AccountRecord>(
            "SELECT * FROM Account WHERE Email = ? AND IsActive = 1",
            NormalizeEmail(email));

        if (account is null)
        {
            return null;
        }

        var expectedHash = Convert.FromBase64String(account.PasswordHash);
        var actualHash = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(account.PasswordSalt),
            account.PasswordIterations,
            HashAlgorithmName.SHA256,
            HashSize));

        return CryptographicOperations.FixedTimeEquals(expectedHash, actualHash)
            ? ToUser(account)
            : null;
    }

    public async Task<IReadOnlyList<AppUser>> GetUsersAsync(string? search = null)
    {
        await InitializeAsync();

        List<AccountRecord> accounts;
        if (string.IsNullOrWhiteSpace(search))
        {
            accounts = await _connection.QueryAsync<AccountRecord>(
                "SELECT * FROM Account ORDER BY Name COLLATE NOCASE");
        }
        else
        {
            var pattern = $"%{EscapeLike(search.Trim())}%";
            accounts = await _connection.QueryAsync<AccountRecord>(
                """
                SELECT * FROM Account
                WHERE Name LIKE ? ESCAPE '\' OR Email LIKE ? ESCAPE '\'
                ORDER BY Name COLLATE NOCASE
                """,
                pattern,
                pattern);
        }

        return accounts.Select(ToUser).ToArray();
    }

    public async Task UpdateUserAsync(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(user.Email);

        var email = NormalizeEmail(user.Email);
        if (user.Id <= 0 || !Enum.IsDefined(user.Role) ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            throw new ArgumentException("Confira o nome, o e-mail e o tipo da conta.", nameof(user));
        }

        await InitializeAsync();
        var duplicate = await _connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Account WHERE Email = ? AND Id <> ?", email, user.Id);
        if (duplicate > 0)
        {
            throw new InvalidOperationException("Este e-mail já está cadastrado.");
        }

        var updated = await _connection.ExecuteAsync(
            "UPDATE Account SET Name = ?, Email = ?, Role = ?, IsActive = ? WHERE Id = ?",
            user.Name.Trim(), email, (int)user.Role, user.IsActive, user.Id);

        if (updated == 0)
        {
            throw new InvalidOperationException("Usuário não encontrado.");
        }
    }

    public async Task<int> CountUsersAsync()
    {
        await InitializeAsync();
        return await _connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Account");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        _initializationLock.Dispose();
    }

    private static AccountRecord CreateDemoAccount(string name, string email, UserRole role)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            "GymTrack123!", salt, PasswordIterations, HashAlgorithmName.SHA256, HashSize);

        return new AccountRecord
        {
            Name = name,
            Email = email,
            Role = (int)role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash),
            PasswordIterations = PasswordIterations
        };
    }

    private static AppUser ToUser(AccountRecord account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Email = account.Email,
        Role = (UserRole)account.Role,
        IsActive = account.IsActive,
        CreatedAt = account.CreatedAt
    };

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    [Table("Account")]
    private sealed class AccountRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int Role { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public string PasswordSalt { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public int PasswordIterations { get; set; }
    }
}
