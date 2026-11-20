using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FinanceDesktop;

internal sealed record AccountBalanceForecast(
    decimal AccountOneNow,
    decimal AccountTwoNow,
    decimal UnassignedNetNow,
    int UnassignedTransactionsNow,
    decimal AccountOneMonthEnd,
    decimal AccountTwoMonthEnd,
    decimal UnassignedNetMonthEnd,
    int UnassignedTransactionsMonthEnd,
    bool CanShowCurrent,
    bool CanShowMonthEnd);

internal sealed class Database
{
    private readonly string _rootDirectory;
    private readonly string _accountsConnectionString;
    private string? _profileConnectionString;

    public Database()
    {
        _rootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VaultFinance");
        Directory.CreateDirectory(_rootDirectory);
        Directory.CreateDirectory(Path.Combine(_rootDirectory, "Profiles"));
        var accountsPath = Path.Combine(_rootDirectory, "accounts.db");
        _accountsConnectionString = new SqliteConnectionStringBuilder { DataSource = accountsPath }.ToString();
        InitializeAccounts();
        MigrateLegacyVault();
    }

    private SqliteConnection Open()
    {
        if (_profileConnectionString is null)
            throw new InvalidOperationException("Unlock or create a local account before accessing its finance data.");
        return new SqliteConnection(_profileConnectionString);
    }

    private SqliteConnection OpenAccounts() => new(_accountsConnectionString);

    private void InitializeAccounts()
    {
        using var db = OpenAccounts();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS LocalAccounts (
                Username TEXT PRIMARY KEY COLLATE NOCASE,
                PasswordHash TEXT NOT NULL
            )
            """;
        command.ExecuteNonQuery();
    }

    private void MigrateLegacyVault()
    {
        var legacyPath = Path.Combine(_rootDirectory, "vault.db");
        if (!File.Exists(legacyPath)) return;

        using var legacy = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath }.ToString());
        legacy.Open();
        using var read = legacy.CreateCommand();
        read.CommandText = "SELECT Value FROM Settings WHERE Key=$key";
        read.Parameters.AddWithValue("$key", "Username");
        var username = read.ExecuteScalar()?.ToString();
        if (string.IsNullOrWhiteSpace(username)) return;
        read.Parameters["$key"].Value = "PasswordHash";
        var hash = read.ExecuteScalar()?.ToString();
        if (string.IsNullOrWhiteSpace(hash)) return;

        var destination = GetProfilePath(username);
        if (!File.Exists(destination)) File.Copy(legacyPath, destination);
        using var accounts = OpenAccounts();
        accounts.Open();
        using var register = accounts.CreateCommand();
        register.CommandText = "INSERT OR IGNORE INTO LocalAccounts(Username, PasswordHash) VALUES($username, $hash)";
        register.Parameters.AddWithValue("$username", username.Trim());
        register.Parameters.AddWithValue("$hash", hash);
        register.ExecuteNonQuery();
    }

    private string GetProfilePath(string username)
    {
        var key = username.Trim().ToUpperInvariant();
        var fileName = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))) + ".db";
        return Path.Combine(_rootDirectory, "Profiles", fileName);
    }

    private void ActivateProfile(string username)
    {
        var path = GetProfilePath(username);
        if (!File.Exists(path))
            throw new InvalidOperationException("This account's local finance database is missing. No replacement database was created.");
        InitializeProfile(path);
    }

    private void InitializeProfile(string profilePath)
    {
        _profileConnectionString = new SqliteConnectionStringBuilder { DataSource = profilePath }.ToString();
        using var db = Open();
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
                MonthKey TEXT NOT NULL,
                EntryDate TEXT NOT NULL DEFAULT '',
                IsRecurring INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Budgets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MonthKey TEXT NOT NULL,
                Category TEXT NOT NULL,
                LimitAmount TEXT NOT NULL,
                IsRecurring INTEGER NOT NULL DEFAULT 1,
                UNIQUE(MonthKey, Category)
            );
            CREATE TABLE IF NOT EXISTS PlanItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Scope TEXT NOT NULL,
                PeriodKey TEXT NOT NULL,
                Title TEXT NOT NULL,
                Completed INTEGER NOT NULL DEFAULT 0,
                IsRecurring INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS Categories (
                Type TEXT NOT NULL,
                Name TEXT NOT NULL,
                PRIMARY KEY (Type, Name)
            );
            """;
        command.ExecuteNonQuery();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var schema = db.CreateCommand())
        {
            schema.CommandText = "PRAGMA table_info(Transactions)";
            using var reader = schema.ExecuteReader();
            while (reader.Read()) columns.Add(reader.GetString(1));
        }
        if (!columns.Contains("EntryDate"))
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE Transactions ADD COLUMN EntryDate TEXT NOT NULL DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
        if (!columns.Contains("Account"))
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE Transactions ADD COLUMN Account TEXT NOT NULL DEFAULT 'UNASSIGNED'";
            migrate.ExecuteNonQuery();
        }
        if (!columns.Contains("TransferTo"))
        {
            using var migrate = db.CreateCommand();
            migrate.CommandText = "ALTER TABLE Transactions ADD COLUMN TransferTo TEXT NOT NULL DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
        using (var migrate = db.CreateCommand())
        {
            migrate.CommandText = """
                UPDATE Transactions
                SET EntryDate=MonthKey || '-01'
                WHERE EntryDate=''
                """;
            migrate.ExecuteNonQuery();
        }

        SeedCategories(db);
    }

    private static void SeedCategories(SqliteConnection db)
    {
        var defaults = new[]
        {
            ("EXPENSE", "Food"), ("EXPENSE", "Clothing"), ("EXPENSE", "Accessories"),
            ("EXPENSE", "Rent"), ("EXPENSE", "Electricity"), ("EXPENSE", "Mobile Recharge"),
            ("EXPENSE", "Internet Recharge"), ("EXPENSE", "Parents"), ("EXPENSE", "EMI"),
            ("EXPENSE", "Transport"), ("EXPENSE", "Healthcare"), ("EXPENSE", "Entertainment"),
            ("EXPENSE", "Other"), ("INCOME", "Salary"), ("INCOME", "Other Income"),
            ("SAVINGS", "Savings"), ("SAVINGS", "Investment"), ("TRANSFER", "Account transfer")
        };
        using var command = db.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Categories(Type, Name) VALUES($type, $name)";
        var type = command.Parameters.Add("$type", SqliteType.Text);
        var name = command.Parameters.Add("$name", SqliteType.Text);
        foreach (var category in defaults)
        {
            type.Value = category.Item1;
            name.Value = category.Item2;
            command.ExecuteNonQuery();
        }
    }

    public bool HasAccount()
    {
        using var db = OpenAccounts();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM LocalAccounts";
        return Convert.ToInt64(command.ExecuteScalar()) > 0;
    }

    public void CreateAccount(string username, string password)
    {
        username = username.Trim();
        if (username.Length < 3) throw new ArgumentException("Username must be at least 3 characters.");
        if (password.Length < 14) throw new ArgumentException("Password must be at least 14 characters.");

        using var db = OpenAccounts();
        db.Open();
        using (var exists = db.CreateCommand())
        {
            exists.CommandText = "SELECT COUNT(*) FROM LocalAccounts WHERE Username=$username";
            exists.Parameters.AddWithValue("$username", username);
            if (Convert.ToInt64(exists.ExecuteScalar()) > 0)
                throw new InvalidOperationException("That username already has a local account.");
        }

        var profilePath = GetProfilePath(username);
        if (File.Exists(profilePath))
            throw new InvalidOperationException("A profile file already exists for that username. Choose another username.");
        InitializeProfile(profilePath);
        using var register = db.CreateCommand();
        register.CommandText = "INSERT INTO LocalAccounts(Username, PasswordHash) VALUES($username, $hash)";
        register.Parameters.AddWithValue("$username", username);
        register.Parameters.AddWithValue("$hash", HashPassword(password));
        register.ExecuteNonQuery();
    }

    public bool VerifyAccount(string username, string password)
    {
        using var db = OpenAccounts();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Username, PasswordHash FROM LocalAccounts WHERE Username=$username";
        command.Parameters.AddWithValue("$username", username.Trim());
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return false;
        var storedUsername = reader.GetString(0);
        var hash = reader.GetString(1);
        reader.Close();
        if (!VerifyHash(password, hash)) return false;
        ActivateProfile(storedUsername);
        return true;
    }

    private static void SetSetting(SqliteConnection db, SqliteTransaction transaction, string key, string value)
    {
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR REPLACE INTO Settings(Key, Value) VALUES($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToHexString(salt)}.{Convert.ToHexString(hash)}";
    }

    private static bool VerifyHash(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 2) return false;
        try
        {
            var salt = Convert.FromHexString(parts[0]);
            var expected = Convert.FromHexString(parts[1]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 100_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public void AddTransaction(decimal amount, string type, string category, string description,
        DateTime entryDate, string budgetMonth, bool recurring, string account, string transferTo = "")
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO Transactions(Amount, Type, Category, Description, MonthKey, EntryDate, IsRecurring, CreatedAt, Account, TransferTo)
            VALUES($amount, $type, $category, $description, $monthKey, $entryDate, $recurring, $created, $account, $transferTo);
            """;
        command.Parameters.AddWithValue("$amount", amount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$monthKey", budgetMonth);
        command.Parameters.AddWithValue("$entryDate", entryDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$recurring", recurring ? 1 : 0);
        command.Parameters.AddWithValue("$created", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("$account", account);
        command.Parameters.AddWithValue("$transferTo", transferTo);
        command.ExecuteNonQuery();
    }

    public List<Dictionary<string, string>> GetTransactions(string monthKey)
    {
        var result = new List<Dictionary<string, string>>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Id, Amount, Type, Category, Description, IsRecurring, EntryDate, MonthKey, Account, TransferTo
            FROM Transactions WHERE MonthKey=$monthKey
            ORDER BY Id DESC
            """;
        command.Parameters.AddWithValue("$monthKey", monthKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Dictionary<string, string>
            {
                ["Id"] = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                ["Amount"] = reader.GetString(1),
                ["Type"] = reader.GetString(2),
                ["Category"] = reader.GetString(3),
                ["Description"] = reader.GetString(4),
                ["Recurring"] = reader.GetInt64(5) == 1 ? "Yes" : "",
                ["Date"] = reader.GetString(6),
                ["Month"] = reader.GetString(7),
                ["Account"] = reader.GetString(8),
                ["TransferTo"] = reader.GetString(9)
            });
        }
        return result;
    }

    public List<Dictionary<string, string>> GetTransactionsForDate(DateTime date)
    {
        var result = new List<Dictionary<string, string>>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Id, Amount, Type, Category, Description, IsRecurring, EntryDate, MonthKey, Account, TransferTo
            FROM Transactions WHERE EntryDate=$date
            ORDER BY Id DESC
            """;
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Dictionary<string, string>
            {
                ["Id"] = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                ["Amount"] = reader.GetString(1),
                ["Type"] = reader.GetString(2),
                ["Category"] = reader.GetString(3),
                ["Description"] = reader.GetString(4),
                ["Recurring"] = reader.GetInt64(5) == 1 ? "Yes" : "",
                ["Date"] = reader.GetString(6),
                ["Month"] = reader.GetString(7),
                ["Account"] = reader.GetString(8),
                ["TransferTo"] = reader.GetString(9)
            });
        }
        return result;
    }

    public List<Dictionary<string, string>> GetTransactionsForYear(int year)
    {
        var result = new List<Dictionary<string, string>>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Id, Amount, Type, Category, Description, IsRecurring, EntryDate, MonthKey, Account, TransferTo
            FROM Transactions WHERE MonthKey LIKE $year || '-%'
            ORDER BY MonthKey, EntryDate, Id DESC
            """;
        command.Parameters.AddWithValue("$year", year.ToString(CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Dictionary<string, string>
            {
                ["Id"] = reader.GetInt64(0).ToString(CultureInfo.InvariantCulture),
                ["Amount"] = reader.GetString(1),
                ["Type"] = reader.GetString(2),
                ["Category"] = reader.GetString(3),
                ["Description"] = reader.GetString(4),
                ["Recurring"] = reader.GetInt64(5) == 1 ? "Yes" : "",
                ["Date"] = reader.GetString(6),
                ["Month"] = reader.GetString(7),
                ["Account"] = reader.GetString(8),
                ["TransferTo"] = reader.GetString(9)
            });
        }
        return result;
    }

    public (decimal BankOne, decimal BankTwo, decimal Investments, DateTime AsOfDate, DateTime AsOfTimestamp) GetOpeningBalances()
    {
        var dateText = GetSetting("OpeningBalanceDate");
        var asOfDate = dateText is null
            ? DateTime.Today
            : DateTime.ParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var timestampText = GetSetting("OpeningBalanceTimestamp");
        var asOfTimestamp = timestampText is null
            ? asOfDate.Date.AddDays(1).AddTicks(-1)
            : DateTime.Parse(timestampText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
        return (GetSettingAmount("OpeningBank"), GetSettingAmount("OpeningSavings"),
            GetSettingAmount("OpeningInvestments"), asOfDate, asOfTimestamp);
    }

    private string? GetSetting(string key)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key=$key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar()?.ToString();
    }

    private decimal GetSettingAmount(string key)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key=$key";
        command.Parameters.AddWithValue("$key", key);
        var value = command.ExecuteScalar()?.ToString();
        return value is null ? 0 : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public void SaveOpeningBalances(decimal bankOne, decimal bankTwo, decimal investments, DateTime asOfDate)
    {
        using var db = Open();
        db.Open();
        using var transaction = db.BeginTransaction();
        SetSetting(db, transaction, "OpeningBank", bankOne.ToString(CultureInfo.InvariantCulture));
        SetSetting(db, transaction, "OpeningSavings", bankTwo.ToString(CultureInfo.InvariantCulture));
        SetSetting(db, transaction, "OpeningInvestments", investments.ToString(CultureInfo.InvariantCulture));
        SetSetting(db, transaction, "OpeningBalanceDate", asOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        var asOfTimestamp = asOfDate.Date == DateTime.Today
            ? DateTime.Now
            : asOfDate.Date.AddDays(1).AddTicks(-1);
        SetSetting(db, transaction, "OpeningBalanceTimestamp", asOfTimestamp.ToString("O", CultureInfo.InvariantCulture));
        transaction.Commit();
    }

    public decimal GetInvestedSoFar()
    {
        var opening = GetOpeningBalances();
        var total = opening.Investments;
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Amount FROM Transactions
            WHERE Type='SAVINGS'
                AND (EntryDate > $asOfDate
                    OR (EntryDate = $asOfDate AND CreatedAt > $asOfTimestamp))
            """;
        command.Parameters.AddWithValue("$asOfDate", opening.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asOfTimestamp", opening.AsOfTimestamp.ToString("O", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        while (reader.Read())
            total += decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
        return total;
    }

    public AccountBalanceForecast GetAccountBalanceForecast(DateTime monthEnd)
    {
        var opening = GetOpeningBalances();
        var canShowCurrent = opening.AsOfDate.Date <= DateTime.Today;
        var canShowMonthEnd = monthEnd.Date >= opening.AsOfDate.Date;
        var current = canShowCurrent
            ? CalculateAccountBalances(opening, DateTime.Today)
            : (0m, 0m, 0m, 0);
        var projected = canShowMonthEnd
            ? CalculateAccountBalances(opening, monthEnd.Date)
            : (0m, 0m, 0m, 0);
        return new AccountBalanceForecast(
            current.Item1, current.Item2, current.Item3, current.Item4,
            projected.Item1, projected.Item2, projected.Item3, projected.Item4,
            canShowCurrent, canShowMonthEnd);
    }

    private (decimal AccountOne, decimal AccountTwo, decimal UnassignedNet, int UnassignedCount)
        CalculateAccountBalances(
            (decimal BankOne, decimal BankTwo, decimal Investments, DateTime AsOfDate, DateTime AsOfTimestamp) opening,
            DateTime throughDate)
    {
        var accountOne = opening.BankOne;
        var accountTwo = opening.BankTwo;
        decimal unassignedNet = 0;
        var unassignedCount = 0;
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Amount, Type, Account, TransferTo
            FROM Transactions
            WHERE EntryDate <= $throughDate
                AND (EntryDate > $asOfDate
                    OR (EntryDate = $asOfDate AND CreatedAt > $asOfTimestamp))
            """;
        command.Parameters.AddWithValue("$throughDate", throughDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asOfDate", opening.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$asOfTimestamp", opening.AsOfTimestamp.ToString("O", CultureInfo.InvariantCulture));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var amount = decimal.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
            var type = reader.GetString(1);
            var source = reader.GetString(2);
            var destination = reader.GetString(3);
            if (source == "UNASSIGNED" || destination == "UNASSIGNED") unassignedCount++;

            if (type == "TRANSFER")
            {
                ApplyAccountDelta(source, -amount, ref accountOne, ref accountTwo, ref unassignedNet);
                ApplyAccountDelta(destination, amount, ref accountOne, ref accountTwo, ref unassignedNet);
                continue;
            }

            var delta = type == "INCOME" ? amount : type is "EXPENSE" or "SAVINGS" ? -amount : 0;
            ApplyAccountDelta(source, delta, ref accountOne, ref accountTwo, ref unassignedNet);
        }
        return (accountOne, accountTwo, unassignedNet, unassignedCount);
    }

    private static void ApplyAccountDelta(
        string account, decimal amount, ref decimal accountOne, ref decimal accountTwo, ref decimal unassignedNet)
    {
        switch (account)
        {
            case "BANK1": accountOne += amount; break;
            case "BANK2": accountTwo += amount; break;
            case "UNASSIGNED": unassignedNet += amount; break;
        }
    }

    public void DeleteTransaction(long id)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM Transactions WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateTransaction(long id, decimal amount, string type, string category, string description,
        DateTime entryDate, string budgetMonth, bool recurring, string account, string transferTo = "")
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            UPDATE Transactions
            SET Amount=$amount, Type=$type, Category=$category,
                Description=$description, MonthKey=$monthKey, EntryDate=$entryDate, IsRecurring=$recurring,
                Account=$account, TransferTo=$transferTo
            WHERE Id=$id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$amount", amount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$monthKey", budgetMonth);
        command.Parameters.AddWithValue("$entryDate", entryDate.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$recurring", recurring ? 1 : 0);
        command.Parameters.AddWithValue("$account", account);
        command.Parameters.AddWithValue("$transferTo", transferTo);
        command.ExecuteNonQuery();
    }

    public void RolloverMonth(string sourceMonth, string targetMonth)
    {
        var sourceMonthDate = DateTime.ParseExact(sourceMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var salaryPayDate = new DateTime(sourceMonthDate.Year, sourceMonthDate.Month,
            DateTime.DaysInMonth(sourceMonthDate.Year, sourceMonthDate.Month));
        while (salaryPayDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            salaryPayDate = salaryPayDate.AddDays(-1);

        using var db = Open();
        db.Open();
        using var transaction = db.BeginTransaction();
        using (var recurring = db.CreateCommand())
        {
            recurring.Transaction = transaction;
            recurring.CommandText = """
                INSERT INTO Transactions(Amount, Type, Category, Description, MonthKey, EntryDate, IsRecurring, CreatedAt, Account, TransferTo)
                SELECT source.Amount, source.Type, source.Category, source.Description,
                    $targetMonth,
                    CASE
                        WHEN source.Type='INCOME' AND source.Category='Salary' THEN $salaryPayDate
                        ELSE $targetMonth || '-' || printf('%02d', min(
                            CASE WHEN length(source.EntryDate)=10
                                THEN CAST(substr(source.EntryDate, 9, 2) AS INTEGER)
                                ELSE 1
                            END,
                            CAST(strftime('%d', date($targetMonth || '-01', '+1 month', '-1 day')) AS INTEGER)
                        ))
                    END,
                    1, $created, source.Account, source.TransferTo
                FROM Transactions source
                WHERE source.MonthKey=$sourceMonth AND source.IsRecurring=1
                    AND NOT EXISTS (
                        SELECT 1 FROM Transactions target
                        WHERE target.MonthKey=$targetMonth AND target.IsRecurring=1
                            AND target.Type=source.Type AND target.Category=source.Category
                            AND target.Description=source.Description AND target.Amount=source.Amount
                            AND target.Account=source.Account AND target.TransferTo=source.TransferTo
                    )
                """;
            recurring.Parameters.AddWithValue("$targetMonth", targetMonth);
            recurring.Parameters.AddWithValue("$sourceMonth", sourceMonth);
            recurring.Parameters.AddWithValue("$salaryPayDate", salaryPayDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            recurring.Parameters.AddWithValue("$created", DateTime.Now.ToString("O"));
            recurring.ExecuteNonQuery();
        }
        using (var budgets = db.CreateCommand())
        {
            budgets.Transaction = transaction;
            budgets.CommandText = """
                INSERT OR IGNORE INTO Budgets(MonthKey, Category, LimitAmount, IsRecurring)
                SELECT $targetMonth, Category, LimitAmount, 1
                FROM Budgets WHERE MonthKey=$sourceMonth AND IsRecurring=1
                """;
            budgets.Parameters.AddWithValue("$targetMonth", targetMonth);
            budgets.Parameters.AddWithValue("$sourceMonth", sourceMonth);
            budgets.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void EnsureRecurringThrough(string targetMonth)
    {
        var target = DateTime.ParseExact(targetMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        string? firstRecurringMonth;
        using (var db = Open())
        {
            db.Open();
            using var command = db.CreateCommand();
            command.CommandText = "SELECT MIN(MonthKey) FROM Transactions WHERE IsRecurring=1";
            firstRecurringMonth = command.ExecuteScalar()?.ToString();
        }

        if (string.IsNullOrEmpty(firstRecurringMonth)) return;
        var current = DateTime.ParseExact(firstRecurringMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
        while (current < target)
        {
            var next = current.AddMonths(1);
            RolloverMonth(current.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                next.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            current = next;
        }
    }

    public void SetBudget(string monthKey, string category, decimal limit)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO Budgets(MonthKey, Category, LimitAmount, IsRecurring)
            VALUES($month, $category, $limit, 1)
            ON CONFLICT(MonthKey, Category) DO UPDATE SET LimitAmount=excluded.LimitAmount
            """;
        command.Parameters.AddWithValue("$month", monthKey);
        command.Parameters.AddWithValue("$category", category);
        command.Parameters.AddWithValue("$limit", limit.ToString(CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public List<(string Category, decimal Limit)> GetBudgets(string monthKey)
    {
        var result = new List<(string, decimal)>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Category, LimitAmount FROM Budgets WHERE MonthKey=$month";
        command.Parameters.AddWithValue("$month", monthKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0),
                decimal.Parse(reader.GetString(1), CultureInfo.InvariantCulture)));
        }
        return result;
    }

    public List<string> GetCategories(string type)
    {
        var result = new List<string>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT Name FROM Categories WHERE Type=$type ORDER BY Name COLLATE NOCASE";
        command.Parameters.AddWithValue("$type", type);
        using var reader = command.ExecuteReader();
        while (reader.Read()) result.Add(reader.GetString(0));
        return result;
    }

    public void AddCategory(string type, string name)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO Categories(Type, Name) VALUES($type, $name)";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name.Trim());
        command.ExecuteNonQuery();
    }

    public void EnsureCategory(string type, string name)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Categories(Type, Name) VALUES($type, $name)";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name.Trim());
        command.ExecuteNonQuery();
    }

    public void RenameCategory(string type, string oldName, string newName)
    {
        using var db = Open();
        db.Open();
        using var transaction = db.BeginTransaction();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE Categories SET Name=$new WHERE Type=$type AND Name=$old";
            command.Parameters.AddWithValue("$new", newName.Trim());
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$old", oldName);
            command.ExecuteNonQuery();
        }
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "UPDATE Transactions SET Category=$new WHERE Type=$type AND Category=$old";
            command.Parameters.AddWithValue("$new", newName.Trim());
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$old", oldName);
            command.ExecuteNonQuery();
        }
        if (type == "EXPENSE")
        {
            using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Budgets SET Category=$new WHERE Category=$old";
            command.Parameters.AddWithValue("$new", newName.Trim());
            command.Parameters.AddWithValue("$old", oldName);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public void DeleteCategory(string type, string name)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            DELETE FROM Categories
            WHERE Type=$type AND Name=$name
                AND NOT EXISTS (
                    SELECT 1 FROM Transactions
                    WHERE Type=$type AND Category=$name
                )
                AND (
                    $type != 'EXPENSE'
                    OR NOT EXISTS (SELECT 1 FROM Budgets WHERE Category=$name)
                )
            """;
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name);
        if (command.ExecuteNonQuery() == 0)
            throw new InvalidOperationException("This category is used by existing transactions and cannot be deleted. Rename it instead.");
    }

    public void AddPlan(string scope, string periodKey, string title, bool recurring)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            INSERT INTO PlanItems(Scope, PeriodKey, Title, Completed, IsRecurring)
            VALUES($scope, $period, $title, 0, $recurring)
            """;
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$period", periodKey);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$recurring", recurring ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public List<(long Id, string Title, bool Completed, bool Recurring)> GetPlans(string scope, string periodKey)
    {
        var result = new List<(long, string, bool, bool)>();
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            SELECT Id, Title, Completed, IsRecurring FROM PlanItems
            WHERE Scope=$scope AND PeriodKey=$period
            ORDER BY Id
            """;
        command.Parameters.AddWithValue("$scope", scope);
        command.Parameters.AddWithValue("$period", periodKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add((reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2) == 1, reader.GetInt64(3) == 1));
        return result;
    }

    public void TogglePlan(long id)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "UPDATE PlanItems SET Completed=CASE Completed WHEN 1 THEN 0 ELSE 1 END WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void DeletePlan(long id)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM PlanItems WHERE Id=$id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdatePlan(long id, string title, bool recurring)
    {
        using var db = Open();
        db.Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            UPDATE PlanItems
            SET Title=$title, IsRecurring=$recurring
            WHERE Id=$id
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$title", title);
        command.Parameters.AddWithValue("$recurring", recurring ? 1 : 0);
        command.ExecuteNonQuery();
    }
}
