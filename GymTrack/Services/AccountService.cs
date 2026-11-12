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
            await _connection.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("Account").Select(column => column.Name).ToHashSet();
                if (!columns.Contains(nameof(AccountRecord.Phone)))
                    connection.Execute("ALTER TABLE Account ADD COLUMN Phone TEXT NOT NULL DEFAULT ''");
                if (!columns.Contains(nameof(AccountRecord.JobTitle)))
                    connection.Execute("ALTER TABLE Account ADD COLUMN JobTitle TEXT NOT NULL DEFAULT ''");
                if (!columns.Contains(nameof(AccountRecord.GymName)))
                    connection.Execute("ALTER TABLE Account ADD COLUMN GymName TEXT NOT NULL DEFAULT ''");
                if (!columns.Contains(nameof(AccountRecord.NotificationsEnabled)))
                    connection.Execute("ALTER TABLE Account ADD COLUMN NotificationsEnabled INTEGER NOT NULL DEFAULT 1");
            });

            var seeded = await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM AccountMetadata WHERE Key = 'DemoAccountsSeeded'");

            if (seeded == 0)
            {
                var demoAccounts = await Task.Run(() => new[]
                {
                    CreateAccount("Maria Silva", "consumidor@gymtrack.app", UserRole.Consumer, "GymTrack123!"),
                    CreateAccount("Equipe GymTrack", "funcionario@gymtrack.app", UserRole.Employee, "GymTrack123!")
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

        return await Task.Run(() => MatchesPassword(account, password)) ? ToUser(account) : null;
    }

    public async Task<AppUser> RegisterConsumerAsync(string name, string email, string password)
    {
        var trimmedName = ValidateName(name);
        var normalizedEmail = ValidateEmail(email);
        ValidatePassword(password);

        await InitializeAsync();
        if (await _connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Account WHERE Email = ?", normalizedEmail) != 0)
        {
            throw new InvalidOperationException("Este e-mail já está cadastrado.");
        }

        var account = await Task.Run(() => CreateAccount(
            trimmedName, normalizedEmail, UserRole.Consumer, password));
        if (await _connection.InsertAsync(account, "OR IGNORE") != 1)
        {
            throw new InvalidOperationException("Este e-mail já está cadastrado.");
        }

        return ToUser(account);
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

    public Task<AppUser> UpdatePersonalDetailsAsync(int accountId, string name, string email, string? phone)
    {
        var trimmedName = ValidateName(name);
        var normalizedEmail = ValidateEmail(email);
        var trimmedPhone = ValidateOptionalValue(phone, 30, "Telefone");

        return UpdateProfileAsync(accountId, connection =>
        {
            if (connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Account WHERE Email = ? AND Id <> ?", normalizedEmail, accountId) != 0)
            {
                throw new InvalidOperationException("Este e-mail já está cadastrado.");
            }

            return connection.Execute(
                "UPDATE Account SET Name = ?, Email = ?, Phone = ? WHERE Id = ? AND IsActive = 1",
                trimmedName, normalizedEmail, trimmedPhone, accountId);
        });
    }

    public Task<AppUser> UpdateProfessionalDetailsAsync(int accountId, string? jobTitle, string? gymName)
    {
        var trimmedJobTitle = ValidateOptionalValue(jobTitle, 100, "Cargo");
        var trimmedGymName = ValidateOptionalValue(gymName, 100, "Academia");
        return UpdateProfileAsync(accountId, connection => connection.Execute(
            "UPDATE Account SET JobTitle = ?, GymName = ? WHERE Id = ? AND IsActive = 1 AND Role = ?",
            trimmedJobTitle, trimmedGymName, accountId, (int)UserRole.Employee));
    }

    public Task<AppUser> SetNotificationsEnabledAsync(int accountId, bool enabled) =>
        UpdateProfileAsync(accountId, connection => connection.Execute(
            "UPDATE Account SET NotificationsEnabled = ? WHERE Id = ? AND IsActive = 1", enabled, accountId));

    public async Task ChangePasswordAsync(int accountId, string currentPassword, string newPassword)
    {
        ValidatePassword(newPassword);
        if (string.IsNullOrWhiteSpace(currentPassword))
            throw new ArgumentException("Informe a senha atual.", nameof(currentPassword));

        await InitializeAsync();
        var account = await _connection.FindWithQueryAsync<AccountRecord>(
            "SELECT * FROM Account WHERE Id = ? AND IsActive = 1", accountId);
        if (account is null || !await Task.Run(() => MatchesPassword(account, currentPassword)))
            throw new InvalidOperationException("A senha atual está incorreta.");

        var (salt, hash) = await Task.Run(() => CreatePasswordHash(newPassword));
        var updated = await _connection.ExecuteAsync(
            """
            UPDATE Account SET PasswordSalt = ?, PasswordHash = ?, PasswordIterations = ?
            WHERE Id = ? AND IsActive = 1 AND PasswordSalt = ? AND PasswordHash = ? AND PasswordIterations = ?
            """,
            salt, hash, PasswordIterations, accountId, account.PasswordSalt, account.PasswordHash, account.PasswordIterations);
        if (updated == 0)
            throw new InvalidOperationException("A conta foi alterada. Tente novamente.");
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        _initializationLock.Dispose();
    }

    private async Task<AppUser> UpdateProfileAsync(int accountId, Func<SQLiteConnection, int> update)
    {
        await InitializeAsync();
        AccountRecord? updated = null;
        await _connection.RunInTransactionAsync(connection =>
        {
            if (update(connection) == 0)
                throw new InvalidOperationException("Não foi possível atualizar os dados desta conta.");

            updated = connection.Get<AccountRecord>(accountId);
        });
        return ToUser(updated!);
    }

    private static AccountRecord CreateAccount(string name, string email, UserRole role, string password)
    {
        var (salt, hash) = CreatePasswordHash(password);
        return new AccountRecord
        {
            Name = name,
            Email = email,
            Role = (int)role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            PasswordSalt = salt,
            PasswordHash = hash,
            PasswordIterations = PasswordIterations
        };
    }

    private static (string Salt, string Hash) CreatePasswordHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = HashPassword(password, salt, PasswordIterations);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    private static bool MatchesPassword(AccountRecord account, string password)
    {
        var expected = Convert.FromBase64String(account.PasswordHash);
        var salt = Convert.FromBase64String(account.PasswordSalt);
        var actual = HashPassword(password, salt, account.PasswordIterations);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private static byte[] HashPassword(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

    private static AppUser ToUser(AccountRecord account) => new()
    {
        Id = account.Id,
        Name = account.Name,
        Email = account.Email,
        Phone = account.Phone,
        JobTitle = account.JobTitle,
        GymName = account.GymName,
        NotificationsEnabled = account.NotificationsEnabled,
        Role = (UserRole)account.Role,
        IsActive = account.IsActive,
        CreatedAt = account.CreatedAt
    };

    private static string ValidateName(string? name)
    {
        var trimmedName = name?.Trim();
        if (trimmedName is null || trimmedName.Length is < 2 or > 100)
            throw new ArgumentException("Informe um nome de 2 a 100 caracteres.", nameof(name));
        return trimmedName;
    }

    private static string ValidateEmail(string? email)
    {
        var normalized = email is null ? string.Empty : NormalizeEmail(email);
        if (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var address) || address.Address != normalized)
            throw new ArgumentException("Informe um e-mail válido.", nameof(email));
        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
            throw new ArgumentException("A senha deve ter de 8 a 128 caracteres.", nameof(password));
    }

    private static string ValidateOptionalValue(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length > maxLength)
            throw new ArgumentException($"O campo {field} deve ter até {maxLength} caracteres.");
        return trimmed;
    }

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

        public string Phone { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public string GymName { get; set; } = string.Empty;

        public bool NotificationsEnabled { get; set; } = true;

        public int Role { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public string PasswordSalt { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public int PasswordIterations { get; set; }
    }
}
