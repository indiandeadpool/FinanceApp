using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceMobile;

internal sealed record FinanceTransaction(
    long Id,
    decimal Amount,
    string Type,
    string Category,
    string Description,
    DateTime EntryDate,
    string MonthKey,
    string Account);

internal sealed record StartingBalances(decimal AccountOne, decimal AccountTwo, DateTime AsOfDate);

internal sealed record CurrentBalances(decimal AccountOne, decimal AccountTwo);

internal sealed class LocalFinanceStore
{
    private const int PasswordIterations = 100_000;
    private readonly string _root = FileSystem.AppDataDirectory;
    private readonly string _accountsPath;
    private string? _activeProfilePath;

    public LocalFinanceStore()
    {
        Directory.CreateDirectory(_root);
        _accountsPath = Path.Combine(_root, "accounts.db");
        using var db = Open(_accountsPath);
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS LocalAccounts (
                Username TEXT PRIMARY KEY COLLATE NOCASE,
                Salt TEXT NOT NULL,
                PasswordHash TEXT NOT NULL
            )
            """;
        command.ExecuteNonQuery();
    }

    public string CreateAccount(string username, string password)
    {
        username = username.Trim();
        if (username.Length < 3)
            throw new ArgumentException("Username must be at least 3 characters.");
        if (password.Length < 14)
            throw new ArgumentException("Password must be at least 14 characters.");

        using var db = Open(_accountsPath);
        db.Open();
        using (var exists = db.CreateCommand())
        {
            exists.CommandText = "SELECT COUNT(*) FROM LocalAccounts WHERE Username=$username";
            exists.Parameters.AddWithValue("$username", username);
            if (Convert.ToInt64(exists.ExecuteScalar(), CultureInfo.InvariantCulture) > 0)
                throw new InvalidOperationException("That username already has a local account on this device.");
        }

        var profilePath = GetProfilePath(username);
        if (File.Exists(profilePath))
            throw new InvalidOperationException("A profile file already exists for that username. Choose another username.");
        InitializeProfile(profilePath);

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(password, salt);
        using var register = db.CreateCommand();
        register.CommandText = "INSERT INTO LocalAccounts(Username, Salt, PasswordHash) VALUES($username, $salt, $hash)";
        register.Parameters.AddWithValue("$username", username);
        register.Parameters.AddWithValue("$salt", Convert.ToHexString(salt));
        register.Parameters.AddWithValue("$hash", Convert.ToHexString(hash));
        register.ExecuteNonQuery();
        _activeProfilePath = profilePath;
        return username;
    }

    public string Authenticate(string username, string password)
    {
        using var db = Open(_accountsPath);
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Username, Salt, PasswordHash FROM LocalAccounts WHERE Username=$username";
        command.Parameters.AddWithValue("$username", username.Trim());
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            throw new InvalidOperationException("Invalid username or password.");

        var storedUsername = reader.GetString(0);
        var salt = Convert.FromHexString(reader.GetString(1));
        var expectedHash = Convert.FromHexString(reader.GetString(2));
        var actualHash = HashPassword(password, salt);
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
            throw new InvalidOperationException("Invalid username or password.");

        var profilePath = GetProfilePath(storedUsername);
        if (!File.Exists(profilePath))
            throw new InvalidOperationException("This account's local finance data is missing; no replacement profile was created.");
        _activeProfilePath = profilePath;
        return storedUsername;
    }

    public StartingBalances GetStartingBalances()
    {
        using var db = OpenProfile();
        db.Open();
        return new StartingBalances(
            ReadAmount(db, "OpeningBankOne"),
            ReadAmount(db, "OpeningBankTwo"),
            DateTime.ParseExact(ReadSetting(db, "OpeningDate"), "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public void SaveStartingBalances(decimal accountOne, decimal accountTwo, DateTime asOfDate)
    {
        if (accountOne < 0 || accountTwo < 0)
            throw new ArgumentOutOfRangeException(nameof(accountOne), "Starting balances cannot be negative.");

        using var db = OpenProfile();
        db.Open();
        using var transaction = db.BeginTransaction();
        WriteSetting(db, transaction, "OpeningBankOne", accountOne.ToString(CultureInfo.InvariantCulture));
        WriteSetting(db, transaction, "OpeningBankTwo", accountTwo.ToString(CultureInfo.InvariantCulture));
        WriteSetting(db, transaction, "OpeningDate", asOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var timestamp = asOfDate.Date == DateTime.Today
            ? DateTime.Now
            : asOfDate.Date.AddDays(1).AddTicks(-1);
        WriteSetting(db, transaction, "OpeningTimestamp", timestamp.ToString("O", CultureInfo.InvariantCulture));
        transaction.Commit();
    }

    public CurrentBalances GetCurrentBalances()
    {
        var opening = GetStartingBalances();
        using var db = OpenProfile();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Amount, Type, Account FROM Transactions
            WHERE EntryDate <= $today
                AND (EntryDate > $asOfDate
                    OR (EntryDate = $asOfDate AND CreatedAt > $asOfTimestamp))
            """;
        command.Parameters.AddWithValue("$today", DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asOfDate", opening.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asOfTimestamp", ReadSetting(db, "OpeningTimestamp"));

        var accountOne = opening.AccountOne;
        var accountTwo = opening.AccountTwo;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var amount = decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
            var type = reader.GetString(1);
            var account = reader.GetString(2);
            var delta = type == "INCOME" ? amount : -amount;
            if (account == "BANK1") accountOne += delta;
            else if (account == "BANK2") accountTwo += delta;
        }
        return new CurrentBalances(accountOne, accountTwo);
    }

    public List<FinanceTransaction> GetTransactions(string monthKey)
    {
        var result = new List<FinanceTransaction>();
        using var db = OpenProfile();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Id, Amount, Type, Category, Description, EntryDate, MonthKey, Account
            FROM Transactions WHERE MonthKey=$month
            ORDER BY EntryDate DESC, Id DESC
            """;
        command.Parameters.AddWithValue("$month", monthKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new FinanceTransaction(
                reader.GetInt64(0),
                decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTime.ParseExact(reader.GetString(5), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(6),
                reader.GetString(7)));
        }
        return result;
    }

    public void AddTransaction(decimal amount, string type, string category, string description,
        DateTime entryDate, string monthKey, string account)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        if (type is not ("INCOME" or "EXPENSE")) throw new ArgumentException("Unsupported transaction type.", nameof(type));
        if (account is not ("BANK1" or "BANK2")) throw new ArgumentException("Select a savings account.", nameof(account));
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Enter a category and note.");

        using var db = OpenProfile();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO Transactions(Amount, Type, Category, Description, EntryDate, MonthKey, Account, CreatedAt)
            VALUES($amount, $type, $category, $description, $entryDate, $month, $account, $created)
            """;
        command.Parameters.AddWithValue("$amount", amount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$category", category.Trim());
        command.Parameters.AddWithValue("$description", description.Trim());
        command.Parameters.AddWithValue("$entryDate", entryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$month", monthKey);
        command.Parameters.AddWithValue("$account", account);
        command.Parameters.AddWithValue("$created", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public void DeleteTransaction(long id)
    {
        using var db = OpenProfile();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM Transactions WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private string GetProfilePath(string username)
    {
        var profileDirectory = Path.Combine(_root, "Profiles");
        Directory.CreateDirectory(profileDirectory);
        var key = username.Trim().ToUpperInvariant();
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".db";
        return Path.Combine(profileDirectory, fileName);
    }

    private void InitializeProfile(string path)
    {
        using var db = Open(path);
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Amount TEXT NOT NULL,
                Type TEXT NOT NULL,
                Category TEXT NOT NULL,
                Description TEXT NOT NULL,
                EntryDate TEXT NOT NULL,
                MonthKey TEXT NOT NULL,
                Account TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        using var transaction = db.BeginTransaction();
        var now = DateTime.Now;
        WriteSetting(db, transaction, "OpeningBankOne", "0");
        WriteSetting(db, transaction, "OpeningBankTwo", "0");
        WriteSetting(db, transaction, "OpeningDate", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        WriteSetting(db, transaction, "OpeningTimestamp", now.ToString("O", CultureInfo.InvariantCulture));
        transaction.Commit();
    }

    private SqliteConnection OpenProfile()
    {
        if (_activeProfilePath is null)
            throw new InvalidOperationException("Create or unlock a local account first.");
        return Open(_activeProfilePath);
    }

    private static SqliteConnection Open(string path) =>
        new(new SqliteConnectionStringBuilder { DataSource = path }.ToString());

    private static string ReadSetting(SqliteConnection db, string key)
    {
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key=$key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString()
            ?? throw new InvalidOperationException($"Required local setting '{key}' is missing.");
    }

    private static decimal ReadAmount(SqliteConnection db, string key) =>
        decimal.Parse(ReadSetting(db, key), CultureInfo.InvariantCulture);

    private static void WriteSetting(SqliteConnection db, SqliteTransaction transaction, string key, string value)
    {
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO Settings(Key, Value) VALUES($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
}
