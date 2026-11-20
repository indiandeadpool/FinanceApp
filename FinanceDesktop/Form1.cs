using System.ComponentModel;
using System.Globalization;
using System.Drawing.Drawing2D;

namespace FinanceDesktop;

public partial class Form1 : Form
{
    private readonly Database _database = new();
    private static readonly Color Background = Color.FromArgb(11, 18, 38);
    private static readonly Color Surface = Color.FromArgb(27, 38, 67);
    private static readonly Color Muted = Color.FromArgb(166, 181, 207);
    private static readonly Color Green = Color.FromArgb(16, 185, 129);
    private static readonly Color Red = Color.FromArgb(244, 83, 112);
    private static readonly Color Blue = Color.FromArgb(56, 189, 248);
    private static readonly Color Purple = Color.FromArgb(167, 139, 250);

    public Form1()
    {
        InitializeComponent();
        BuildLogin();
    }

    private sealed record EntryTypeOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record BankAccountOption(string Value, string Label)
    {
        public override string ToString() => Label;
    }

    private void BuildLogin()
    {
        Text = "Vault Finance";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(520, 470);
        WindowState = FormWindowState.Normal;
        BackColor = Background;
        Controls.Clear();
        var hasAccounts = _database.HasAccount();
        var creatingAccount = true;
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(48) };
        var title = new Label
        {
            Text = "Vault Finance",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(48, 45)
        };
        var subtitle = new Label
        {
            Text = creatingAccount ? "Create your private local finance account" : "Unlock your local finance vault",
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(48, 90)
        };
        var username = new TextBox { PlaceholderText = "Username", Location = new Point(48, 140), Width = 380, Font = new Font("Segoe UI", 12) };
        var password = new TextBox { PlaceholderText = "Password", Location = new Point(48, 185), Width = 380, Font = new Font("Segoe UI", 12), UseSystemPasswordChar = true };
        var button = MakeButton(creatingAccount ? "Create local account" : "Unlock Vault", Green);
        button.Location = new Point(48, 235);
        button.Size = new Size(380, 42);
        var switchButton = MakeButton("Use an existing account", Color.FromArgb(79, 70, 229));
        switchButton.Location = new Point(48, 285);
        switchButton.Size = new Size(380, 36);
        switchButton.Visible = hasAccounts;
        var error = new Label { Location = new Point(48, 330), Width = 380, ForeColor = Red, Height = 48 };
        var storageNote = new Label
        {
            Text = "Accounts and finance records stay in this Windows user's local app data. Passwords are stored as salted hashes. Do not share the VaultFinance data folder.",
            Location = new Point(48, 382),
            Width = 380,
            Height = 50,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9)
        };
        switchButton.Click += (_, _) =>
        {
            creatingAccount = !creatingAccount;
            subtitle.Text = creatingAccount ? "Create your private local finance account" : "Unlock your local finance vault";
            button.Text = creatingAccount ? "Create local account" : "Unlock Vault";
            switchButton.Text = creatingAccount ? "Use an existing account" : "Create another local account";
            error.Text = "";
        };
        button.Click += (_, _) =>
        {
            try
            {
                if (creatingAccount)
                {
                    _database.CreateAccount(username.Text, password.Text);
                }
                else if (!_database.VerifyAccount(username.Text, password.Text))
                {
                    error.Text = "Invalid username or password.";
                    return;
                }
                BuildDashboard();
            }
            catch (Exception ex)
            {
                error.Text = ex.Message;
            }
        };
        panel.Controls.AddRange([title, subtitle, username, password, button, switchButton, error, storageNote]);
        Controls.Add(panel);
        AcceptButton = button;
    }

    private void BuildDashboard()
    {
        Controls.Clear();
        Text = "Vault Finance — Money overview";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1120, 740);
        BackColor = Background;

        var view = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 95,
            Font = new Font("Segoe UI", 10)
        };
        view.Items.AddRange(["Month", "Year"]);
        view.SelectedIndex = 0;
        var month = new CalendarPicker("MMMM yyyy") { Width = 160 };
        var year = new NumericUpDown
        {
            Minimum = 2000,
            Maximum = 2100,
            Value = DateTime.Today.Year,
            Width = 90,
            Font = new Font("Segoe UI", 10),
            Visible = false
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 13, 14, 0)
        };
        var openingBalances = MakeButton("Starting balances", Color.FromArgb(124, 58, 237));
        var categoriesButton = MakeButton("Categories", Color.FromArgb(79, 70, 229));
        var lockButton = MakeButton("Lock", Color.FromArgb(51, 65, 85));
        actions.Controls.AddRange([openingBalances, categoriesButton, lockButton]);
        lockButton.Click += (_, _) => BuildLogin();

        var title = new Label
        {
            Text = "Money overview",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 14)
        };
        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(245, 15, 0, 0)
        };
        filters.Controls.AddRange([new Label { Text = "VIEW", ForeColor = Muted, AutoSize = true, Padding = new Padding(0, 8, 2, 0) }, view, month, year]);
        var header = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Background };
        header.Controls.Add(actions);
        header.Controls.Add(filters);
        header.Controls.Add(title);

        var summary = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 122,
            Padding = new Padding(18, 8, 18, 12),
            ColumnCount = 6,
            RowCount = 1,
            BackColor = Background
        };
        for (var i = 0; i < 6; i++) summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        var incomeCard = CreateCard("INCOME", Green);
        var expenseCard = CreateCard("SPENDING", Red);
        var savedCard = CreateCard("SAVED THIS PERIOD", Blue);
        var remainingCard = CreateCard("LEFT THIS PERIOD", Color.White);
        var cashCard = CreateCard("BANK CASH ESTIMATE", Color.FromArgb(251, 191, 36));
        var savingsCard = CreateCard("INVESTED SO FAR", Purple);
        summary.Controls.Add(incomeCard.Panel, 0, 0);
        summary.Controls.Add(expenseCard.Panel, 1, 0);
        summary.Controls.Add(savedCard.Panel, 2, 0);
        summary.Controls.Add(remainingCard.Panel, 3, 0);
        summary.Controls.Add(cashCard.Panel, 4, 0);
        summary.Controls.Add(savingsCard.Panel, 5, 0);

        var ledger = CreateLedgerGrid();
        var ledgerTitle = new Label
        {
            Text = "Transactions this month",
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Padding = new Padding(4, 9, 0, 0)
        };
        var transactionsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15), BackColor = Surface };
        transactionsPanel.Controls.Add(ledger);
        transactionsPanel.Controls.Add(ledgerTitle);

        var addIncome = BuildEntryPanel("Add income", "INCOME", false);
        var addSpending = BuildEntryPanel("Add spending / investment", "EXPENSE", true);
        var leftPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Background
        };
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        leftPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        leftPanel.Controls.Add(addIncome.Panel, 0, 0);
        leftPanel.Controls.Add(addSpending.Panel, 0, 1);

        var spendingPanel = BuildSpendingPanel();
        var categoryChart = (Panel)spendingPanel.Tag!;
        var rightStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Background
        };
        rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        rightStack.Controls.Add(transactionsPanel, 0, 0);
        rightStack.Controls.Add(spendingPanel, 0, 1);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8, 0, 8, 12),
            BackColor = Background
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        body.Controls.Add(leftPanel, 0, 0);
        body.Controls.Add(rightStack, 1, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10) };
        var overviewTab = new TabPage("Overview") { BackColor = Background };
        overviewTab.Controls.Add(body);
        tabs.TabPages.Add(overviewTab);
        var trendChart = new FinanceTrendChart { Dock = DockStyle.Fill, BackColor = Surface };
        var trendsTab = new TabPage("Trends") { BackColor = Background, Padding = new Padding(18) };
        var trendsHeading = new Label
        {
            Text = "Income, spending, savings, and monthly remainder",
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            Padding = new Padding(4, 8, 0, 0)
        };
        trendsTab.Controls.Add(trendChart);
        trendsTab.Controls.Add(trendsHeading);
        tabs.TabPages.Add(trendsTab);
        var assistantTab = new TabPage("Assistant") { BackColor = Background, Padding = new Padding(18) };
        var assistantHistory = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            DetectUrls = false
        };
        var assistantInput = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            Height = 74,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            PlaceholderText = "Example: Spent ₹1,200 on transport today"
        };
        var assistantSend = MakeButton("Process transaction", Green);
        var assistantLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(18),
            BackColor = Background
        };
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        assistantLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        assistantLayout.Controls.Add(new Label
        {
            Text = "Offline transaction assistant — matching same-date, same-category entries are increased; otherwise a new entry is added.",
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        }, 0, 0);
        assistantLayout.Controls.Add(assistantHistory, 0, 1);
        assistantLayout.Controls.Add(assistantInput, 0, 2);
        assistantLayout.Controls.Add(assistantSend, 0, 3);
        assistantTab.Controls.Add(assistantLayout);
        tabs.TabPages.Add(assistantTab);

        var accountCards = new[]
        {
            CreateCard("SAVINGS ACCOUNT 1 — NOW", Green),
            CreateCard("SAVINGS ACCOUNT 2 — NOW", Blue),
            CreateCard("COMBINED CASH — NOW", Color.White),
            CreateCard("ACCOUNT 1 — MONTH-END FORECAST", Green),
            CreateCard("ACCOUNT 2 — MONTH-END FORECAST", Blue),
            CreateCard("COMBINED CASH — MONTH-END FORECAST", Color.White)
        };
        var accountGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Background
        };
        for (var i = 0; i < 3; i++) accountGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        for (var i = 0; i < 2; i++) accountGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        for (var i = 0; i < accountCards.Length; i++)
            accountGrid.Controls.Add(accountCards[i].Panel, i % 3, i / 3);
        var accountNote = new Label
        {
            Dock = DockStyle.Top,
            Height = 96,
            Padding = new Padding(24, 18, 24, 0),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10),
            Text = "Current balances use your saved starting balances plus transactions dated through today. Month-end forecasts include transactions dated through the selected period end, including planned future entries. Transfers move money between accounts without changing combined cash. “Applies to month” affects budget and trend totals; transaction date and bank account determine cash movement. The app does not connect to your bank."
        };
        var unassignedNotice = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Padding = new Padding(24, 8, 24, 0),
            ForeColor = Color.FromArgb(251, 191, 36),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Text = ""
        };
        var accountsTab = new TabPage("Accounts") { BackColor = Background };
        accountsTab.Controls.Add(accountGrid);
        accountsTab.Controls.Add(unassignedNotice);
        accountsTab.Controls.Add(accountNote);
        tabs.TabPages.Add(accountsTab);

        var shell = new Panel { Dock = DockStyle.Fill, BackColor = Background };
        shell.Controls.Add(tabs);
        shell.Controls.Add(summary);
        shell.Controls.Add(header);
        Controls.Add(shell);

        var inputs = new[] { addIncome, addSpending };
        var currentRows = new List<Dictionary<string, string>>();
        var isRefreshing = false;
        long? selectedId = null;
        var selectedInputs = addSpending;

        void ClearEdit()
        {
            selectedId = null;
            selectedInputs = addSpending;
            foreach (var input in inputs)
            {
                input.Add.Enabled = true;
                input.Add.Text = GetAddLabel(input, isNew: false);
                input.Save.Enabled = false;
                input.Delete.Enabled = false;
                input.Amount.Value = 0;
                input.Description.Clear();
                input.Recurring.Checked = input.FixedType == "INCOME" && input.Category.Text == "Salary";
            }
        }

        void Refresh()
        {
            var isYear = view.Text == "Year";
            var recurringThrough = isYear
                ? new DateTime((int)year.Value, 12, 1).AddMonths(1)
                : new DateTime(month.Value.Year, month.Value.Month, 1).AddMonths(1);
            _database.EnsureRecurringThrough(recurringThrough.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            var periodRows = isYear
                ? _database.GetTransactionsForYear((int)year.Value)
                : _database.GetTransactions(month.Value.ToString("yyyy-MM"));
            currentRows = periodRows;
            var income = periodRows.Where(r => r["Type"] == "INCOME").Sum(ReadAmount);
            var expenses = periodRows.Where(r => r["Type"] == "EXPENSE").Sum(ReadAmount);
            var saved = periodRows.Where(r => r["Type"] == "SAVINGS").Sum(ReadAmount);
            var opening = _database.GetOpeningBalances();
            var forecastThrough = isYear
                ? new DateTime((int)year.Value, 12, 31)
                : new DateTime(month.Value.Year, month.Value.Month,
                    DateTime.DaysInMonth(month.Value.Year, month.Value.Month));
            var accountBalances = _database.GetAccountBalanceForecast(forecastThrough);
            SetCard(incomeCard, isYear ? $"INCOME IN {year.Value:0}" : "INCOME THIS MONTH", $"₹{income:N2}");
            SetCard(expenseCard, isYear ? $"SPENDING IN {year.Value:0}" : "SPENDING THIS MONTH", $"₹{expenses:N2}");
            SetCard(savedCard, isYear ? "SAVED THIS YEAR" : "SAVED THIS MONTH", $"₹{saved:N2}");
            SetCard(remainingCard, isYear ? "LEFT THIS YEAR" : "LEFT THIS MONTH", $"₹{income - expenses - saved:N2}");
            var currentCash = accountBalances.AccountOneNow + accountBalances.AccountTwoNow + accountBalances.UnassignedNetNow;
            SetCard(cashCard, "BANK BALANCE ESTIMATE", accountBalances.CanShowCurrent
                ? $"₹{currentCash:N2}"
                : "Not available");
            SetCard(savingsCard, "INVESTED SO FAR", $"₹{_database.GetInvestedSoFar():N2}");
            SetCard(accountCards[0], "SAVINGS ACCOUNT 1 — NOW", accountBalances.CanShowCurrent
                ? $"₹{accountBalances.AccountOneNow:N2}" : "Not available");
            SetCard(accountCards[1], "SAVINGS ACCOUNT 2 — NOW", accountBalances.CanShowCurrent
                ? $"₹{accountBalances.AccountTwoNow:N2}" : "Not available");
            SetCard(accountCards[2], "COMBINED CASH — NOW", accountBalances.CanShowCurrent
                ? $"₹{currentCash:N2}" : "Not available");
            SetCard(accountCards[3], "ACCOUNT 1 — MONTH-END FORECAST", accountBalances.CanShowMonthEnd
                ? $"₹{accountBalances.AccountOneMonthEnd:N2}" : "Before starting snapshot");
            SetCard(accountCards[4], "ACCOUNT 2 — MONTH-END FORECAST", accountBalances.CanShowMonthEnd
                ? $"₹{accountBalances.AccountTwoMonthEnd:N2}" : "Before starting snapshot");
            SetCard(accountCards[5], "COMBINED CASH — MONTH-END FORECAST", accountBalances.CanShowMonthEnd
                ? $"₹{accountBalances.AccountOneMonthEnd + accountBalances.AccountTwoMonthEnd + accountBalances.UnassignedNetMonthEnd:N2}"
                : "Before starting snapshot");
            unassignedNotice.Text = accountBalances.UnassignedTransactionsMonthEnd > 0
                ? $"{accountBalances.UnassignedTransactionsMonthEnd} transaction(s) affecting this forecast have no bank account assigned. Edit them to make each account forecast accurate. Unassigned net: ₹{accountBalances.UnassignedNetMonthEnd:N2}."
                : "";

            title.Text = isYear ? $"Year overview — {year.Value:0}" : $"Monthly overview — {month.Value:MMMM yyyy}";
            ledgerTitle.Text = isYear ? $"Monthly totals for {year.Value:0}" : $"Transactions for {month.Value:MMMM yyyy}";
            RebuildLedger(ledger, isYear);
            isRefreshing = true;
            ledger.DataSource = isYear ? BuildYearRows(periodRows, (int)year.Value) : BuildMonthRows(periodRows);
            isRefreshing = false;
            DrawSpendingBreakdown(categoryChart, periodRows, isYear ? "this year" : "this month");
            if (isYear) trendChart.SetYearData((int)year.Value, periodRows);
            else trendChart.SetMonthData(month.Value, periodRows);
        }

        void RefreshCategories(EntryInputs input)
        {
            input.Category.Items.Clear();
            input.Category.Items.AddRange(_database.GetCategories(input.CurrentType).Cast<object>().ToArray());
            if (input.Category.Items.Count == 0) return;
            var preferredCategory = input.CurrentType == "INCOME" ? "Salary" : null;
            input.Category.SelectedItem = preferredCategory is not null &&
                input.Category.Items.Contains(preferredCategory) ? preferredCategory : input.Category.Items[0];
        }

        void SetInputPeriods(DateTime period)
        {
            foreach (var input in inputs)
            {
                input.BudgetMonth.Value = new DateTime(period.Year, period.Month, 1);
                if (input.FixedType == "INCOME")
                {
                    var isSalary = input.Category.Text == "Salary";
                    if (isSalary) SetSalaryPayDate(input);
                    else SetDateWithinAppliedMonth(input);
                    input.Recurring.Checked = isSalary;
                }
                else
                {
                    SetDateWithinAppliedMonth(input);
                }
            }
        }

        void SetDateWithinAppliedMonth(EntryInputs input)
        {
            var appliedMonth = new DateTime(input.BudgetMonth.Value.Year, input.BudgetMonth.Value.Month, 1);
            input.Date.Value = new DateTime(appliedMonth.Year, appliedMonth.Month,
                Math.Min(DateTime.Today.Day, DateTime.DaysInMonth(appliedMonth.Year, appliedMonth.Month)));
        }

        void SetSalaryPayDate(EntryInputs input)
        {
            var appliedMonth = new DateTime(input.BudgetMonth.Value.Year, input.BudgetMonth.Value.Month, 1)
                .AddDays(-1);
            var payDate = new DateTime(appliedMonth.Year, appliedMonth.Month,
                DateTime.DaysInMonth(appliedMonth.Year, appliedMonth.Month));
            while (payDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                payDate = payDate.AddDays(-1);
            input.Date.Value = payDate;
        }

        void AppendAssistant(string speaker, string message)
        {
            assistantHistory.AppendText($"{speaker}: {message}{Environment.NewLine}{Environment.NewLine}");
            assistantHistory.SelectionStart = assistantHistory.TextLength;
            assistantHistory.ScrollToCaret();
        }

        void ProcessAssistantCommand()
        {
            var text = assistantInput.Text.Trim();
            AppendAssistant("You", text);
            var assistantMonth = view.Text == "Month" ? month.Value : DateTime.Today;
            if (!TransactionCommandParser.TryParse(text, assistantMonth, DateTime.Today,
                    _database.GetCategories("EXPENSE"), _database.GetCategories("INCOME"),
                    _database.GetCategories("SAVINGS"), out var parsed, out var error) || parsed is null)
            {
                AppendAssistant("Assistant", error);
                return;
            }

            var matching = _database.GetTransactionsForDate(parsed.EntryDate)
                .Where(record => record["Type"] == parsed.Type &&
                    record["Category"].Equals(parsed.Category, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Dictionary<string, string>? existing = null;
            if (matching.Count == 1)
            {
                existing = matching[0];
            }
            else if (matching.Count > 1)
            {
                using var dialog = new TransactionMatchDialog(matching);
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedId is null)
                {
                    AppendAssistant("Assistant", "No changes made; choose a specific matching transaction to continue.");
                    return;
                }
                existing = matching.First(record => record["Id"] == dialog.SelectedId.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (existing is not null)
            {
                var previousAmount = ReadAmount(existing);
                var updatedAmount = previousAmount + parsed.Amount;
                var entryDate = DateTime.ParseExact(existing["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var repeats = existing["Recurring"] == "Yes" || parsed.IsRecurring;
                _database.UpdateTransaction(long.Parse(existing["Id"], CultureInfo.InvariantCulture), updatedAmount,
                    existing["Type"], existing["Category"], existing["Description"], entryDate,
                    existing["Month"], repeats, existing["Account"], existing["TransferTo"]);
                AppendAssistant("Assistant",
                    $"Increased {existing["Category"]} on {entryDate:dd MMM yyyy} from ₹{previousAmount:N2} to ₹{updatedAmount:N2}. It remains in the {DateTime.ParseExact(existing["Month"] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture):MMMM yyyy} totals.");
            }
            else
            {
                _database.EnsureCategory(parsed.Type, parsed.Category);
                _database.AddTransaction(parsed.Amount, parsed.Type, parsed.Category, parsed.Description,
                    parsed.EntryDate, parsed.MonthKey, parsed.IsRecurring, "UNASSIGNED");
                AppendAssistant("Assistant",
                    $"Added ₹{parsed.Amount:N2} for {parsed.Category} on {parsed.EntryDate:dd MMM yyyy}, applied to {DateTime.ParseExact(parsed.MonthKey + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture):MMMM yyyy}. Its bank account is unassigned; edit it and choose Savings account 1 or 2 for accurate per-account balances.");
            }

            assistantInput.Clear();
            ClearEdit();
            Refresh();
        }

        assistantSend.Click += (_, _) => ProcessAssistantCommand();
        assistantInput.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter && !args.Shift)
            {
                args.SuppressKeyPress = true;
                ProcessAssistantCommand();
            }
        };

        foreach (var input in inputs)
        {
            input.RefreshCategories = () => RefreshCategories(input);
            if (input.KindSelector is not null)
                input.KindSelector.SelectedIndexChanged += (_, _) =>
                {
                    RefreshCategories(input);
                    UpdateAccountControls(input);
                };
            input.Category.SelectedIndexChanged += (_, _) =>
            {
                if (input.FixedType == "INCOME" && selectedId is null)
                {
                    var isSalary = input.Category.Text == "Salary";
                    input.Recurring.Checked = isSalary;
                    if (isSalary) SetSalaryPayDate(input);
                    else SetDateWithinAppliedMonth(input);
                }
            };
            input.BudgetMonth.ValueChanged += (_, _) =>
            {
                if (input.FixedType == "INCOME" && input.Category.Text == "Salary" && selectedId is null)
                    SetSalaryPayDate(input);
            };
            RefreshCategories(input);
            input.Add.Click += (_, _) =>
            {
                if (!TryReadEntry(input, out var type, out var category, out var description, out var amount,
                        out var account, out var transferTo)) return;
                if (selectedId is not null && selectedInputs == input &&
                    MessageBox.Show("Add this as a separate transaction? The selected entry will remain unchanged.",
                        "Add another entry", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                _database.EnsureCategory(type, category);
                _database.AddTransaction(amount, type, category, description, input.Date.Value.Date,
                    input.BudgetMonth.Value.ToString("yyyy-MM"), input.Recurring.Checked, account, transferTo);
                ClearEdit();
                Refresh();
            };
            input.New.Click += (_, _) =>
            {
                ClearEdit();
                SetInputPeriods(view.Text == "Year" ? new DateTime((int)year.Value, 1, 1) : month.Value);
                input.RefreshCategories?.Invoke();
                input.Category.Focus();
            };
            input.Save.Click += (_, _) =>
            {
                if (selectedId is null || selectedInputs != input ||
                    !TryReadEntry(input, out var type, out var category, out var description, out var amount,
                        out var account, out var transferTo)) return;
                _database.EnsureCategory(type, category);
                _database.UpdateTransaction(selectedId.Value, amount, type, category, description, input.Date.Value.Date,
                    input.BudgetMonth.Value.ToString("yyyy-MM"), input.Recurring.Checked, account, transferTo);
                ClearEdit();
                Refresh();
            };
            input.Delete.Click += (_, _) =>
            {
                if (selectedId is null || selectedInputs != input) return;
                if (MessageBox.Show("Delete this transaction?", "Confirm Delete", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes) return;
                _database.DeleteTransaction(selectedId.Value);
                ClearEdit();
                Refresh();
            };
            if (input.KindSelector is not null)
                input.KindSelector.SelectedIndexChanged += (_, _) =>
                {
                    input.Add.Text = GetAddLabel(input, selectedId is not null && selectedInputs == input);
                };
            UpdateAccountControls(input);
        }

        ledger.SelectionChanged += (_, _) =>
        {
            if (isRefreshing || view.Text == "Year") return;
            if (ledger.CurrentRow?.Cells["Id"].Value is not { } rawId ||
                !long.TryParse(rawId.ToString(), out var id)) return;
            var record = currentRows.FirstOrDefault(r => r["Id"] == id.ToString(CultureInfo.InvariantCulture));
            if (record is null) return;
            selectedId = id;
            selectedInputs = record["Type"] == "INCOME" ? addIncome : addSpending;
            var otherInputs = ReferenceEquals(selectedInputs, addIncome) ? addSpending : addIncome;
            otherInputs.Add.Enabled = true;
            otherInputs.Add.Text = GetAddLabel(otherInputs, isNew: false);
            otherInputs.Save.Enabled = false;
            otherInputs.Delete.Enabled = false;
            selectedInputs.Type = record["Type"];
            selectedInputs.RefreshCategories?.Invoke();
            UpdateAccountControls(selectedInputs);
            selectedInputs.Category.Text = record["Category"];
            SelectAccount(selectedInputs.Account, record["Account"]);
            SelectAccount(selectedInputs.TransferTo, record["TransferTo"]);
            selectedInputs.Description.Text = record["Description"];
            selectedInputs.Amount.Value = decimal.Parse(record["Amount"], CultureInfo.InvariantCulture);
            selectedInputs.Date.Value = DateTime.ParseExact(record["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            selectedInputs.BudgetMonth.Value = DateTime.ParseExact(record["Month"] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
            selectedInputs.Recurring.Checked = record["Recurring"] == "Yes";
            selectedInputs.Add.Enabled = true;
            selectedInputs.Add.Text = GetAddLabel(selectedInputs, isNew: true);
            selectedInputs.Save.Enabled = true;
            selectedInputs.Delete.Enabled = true;
        };

        view.SelectedIndexChanged += (_, _) =>
        {
            var isYear = view.Text == "Year";
            month.Visible = !isYear;
            year.Visible = isYear;
            ClearEdit();
            SetInputPeriods(isYear ? new DateTime((int)year.Value, 1, 1) : month.Value);
            Refresh();
        };
        tabs.SelectedIndexChanged += (_, _) =>
        {
            month.Visible = view.Text == "Month";
            year.Visible = view.Text == "Year";
        };
        month.ValueChanged += (_, _) =>
        {
            ClearEdit();
            SetInputPeriods(month.Value);
            Refresh();
        };
        year.ValueChanged += (_, _) =>
        {
            ClearEdit();
            SetInputPeriods(new DateTime((int)year.Value, 1, 1));
            Refresh();
        };
        categoryChart.SizeChanged += (_, _) => DrawSpendingBreakdown(categoryChart, currentRows,
            view.Text == "Year" ? "this year" : "this month");

        categoriesButton.Click += (_, _) =>
        {
            using var dialog = new CategoryManagerDialog(_database);
            dialog.ShowDialog(this);
            foreach (var input in inputs) RefreshCategories(input);
        };
        openingBalances.Click += (_, _) =>
        {
            using var dialog = new OpeningBalancesDialog(_database);
            if (dialog.ShowDialog(this) == DialogResult.OK) Refresh();
        };

        SetInputPeriods(month.Value);
        Refresh();
    }

    private sealed class CalendarPicker : UserControl
    {
        private readonly string _displayFormat;
        private readonly Button _button;
        private readonly MonthCalendar _calendar;
        private readonly ToolStripDropDown _popup;
        private DateTime _value = DateTime.Today;

        public CalendarPicker(string displayFormat)
        {
            _displayFormat = displayFormat;
            Height = 32;
            BackColor = Surface;
            _button = new Button
            {
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Surface,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                AutoEllipsis = true
            };
            _calendar = new MonthCalendar
            {
                FirstDayOfWeek = Day.Monday,
                MaxSelectionCount = 1,
                ShowWeekNumbers = true
            };
            _popup = new ToolStripDropDown();
            _popup.Items.Add(new ToolStripControlHost(_calendar)
            {
                AutoSize = true,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            });
            _button.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 95);
            _button.Click += (_, _) =>
            {
                _calendar.SetDate(_value);
                _popup.Show(_button, new Point(0, _button.Height));
            };
            _calendar.DateSelected += (_, args) =>
            {
                Value = args.Start;
                _popup.Close();
            };
            Controls.Add(_button);
            UpdateButtonText();
        }

        public event EventHandler? ValueChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime Value
        {
            get => _value;
            set
            {
                var date = value.Date;
                if (_value == date) return;
                _value = date;
                UpdateButtonText();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateButtonText() => _button.Text = $"{_value.ToString(_displayFormat, CultureInfo.CurrentCulture)}  ▾";
    }

    private sealed class EntryInputs
    {
        public required Panel Panel { get; init; }
        public required string FixedType { get; init; }
        public required ComboBox Category { get; init; }
        public required ComboBox? KindSelector { get; init; }
        public required ComboBox Account { get; init; }
        public required ComboBox TransferTo { get; init; }
        public required Label TransferArrow { get; init; }
        public required TextBox Description { get; init; }
        public required NumericUpDown Amount { get; init; }
        public required CalendarPicker Date { get; init; }
        public required CalendarPicker BudgetMonth { get; init; }
        public required CheckBox Recurring { get; init; }
        public required Button Add { get; init; }
        public required Button Save { get; init; }
        public required Button Delete { get; init; }
        public required Button New { get; init; }
        public Action? RefreshCategories { get; set; }
        public string Type
        {
            get => KindSelector?.SelectedItem is EntryTypeOption option ? option.Value : FixedType;
            set
            {
                if (KindSelector is null) return;
                KindSelector.SelectedItem = KindSelector.Items.Cast<EntryTypeOption>()
                    .FirstOrDefault(option => option.Value == value);
            }
        }
        public string CurrentType => Type;
    }

    private sealed record SummaryCard(Panel Panel, Label Title, Label Value);

    private EntryInputs BuildEntryPanel(string titleText, string fixedType, bool hasKindSelector)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Surface, Margin = new Padding(0, 0, 0, 12) };
        var title = new Label
        {
            Text = titleText,
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold)
        };
        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = hasKindSelector ? 8 : 7,
            Padding = new Padding(0, 5, 0, 0),
            BackColor = Surface
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        var kind = hasKindSelector ? new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList } : null;
        if (kind is not null)
        {
            kind.Items.AddRange([
                new EntryTypeOption("EXPENSE", "Expense"),
                new EntryTypeOption("SAVINGS", "Invest from bank"),
                new EntryTypeOption("TRANSFER", "Transfer between accounts")
            ]);
            kind.SelectedIndex = 0;
        }
        var category = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDown };
        var account = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        account.Items.AddRange([
            new BankAccountOption("BANK1", "Savings account 1"),
            new BankAccountOption("BANK2", "Savings account 2"),
            new BankAccountOption("UNASSIGNED", "Unassigned")
        ]);
        account.SelectedIndex = 0;
        var transferTo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        transferTo.Items.AddRange([
            new BankAccountOption("BANK1", "Savings account 1"),
            new BankAccountOption("BANK2", "Savings account 2")
        ]);
        transferTo.SelectedIndex = 1;
        var transferArrow = new Label
        {
            Text = "→",
            Width = 24,
            ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        var accounts = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 3, 0, 0)
        };
        accounts.Controls.AddRange([account, transferArrow, transferTo]);
        transferTo.Visible = false;
        var date = new CalendarPicker("dd MMM yyyy") { Dock = DockStyle.Fill };
        var budgetMonth = new CalendarPicker("MMMM yyyy") { Dock = DockStyle.Fill };
        var amount = new NumericUpDown { Dock = DockStyle.Fill, DecimalPlaces = 2, Maximum = 999999999, ThousandsSeparator = true, Minimum = 0 };
        var description = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "e.g. salary, vegetables, rent" };
        var recurring = new CheckBox { Text = "Repeat this entry every month", ForeColor = Color.White, Dock = DockStyle.Fill };
        var row = 0;
        if (kind is not null) AddField(fields, row++, "Entry type", kind);
        AddField(fields, row++, "Category", category);
        AddField(fields, row++, "Bank account", accounts);
        AddField(fields, row++, "Transaction date", date);
        AddField(fields, row++, "Applies to month", budgetMonth);
        AddField(fields, row++, "Amount (₹)", amount);
        AddField(fields, row++, "Note", description);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0) };
        var add = MakeButton(fixedType == "INCOME" ? "Add income" : "Add spending", fixedType == "INCOME" ? Green : Red);
        var save = MakeButton("Save edit", Color.FromArgb(79, 70, 229));
        var delete = MakeButton("Delete", Color.FromArgb(190, 24, 93));
        var newEntry = MakeButton("New entry", Color.FromArgb(51, 65, 95));
        save.Enabled = false;
        delete.Enabled = false;
        buttons.Controls.AddRange([add, save, delete]);
        fields.Controls.Add(recurring, 0, row);
        fields.SetColumnSpan(recurring, 2);
        fields.Controls.Add(buttons, 0, row + 1);
        fields.SetColumnSpan(buttons, 2);
        fields.RowCount = row + 2;
        fields.RowStyles.Clear();
        for (var i = 0; i < row; i++) fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / (row + 2)));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.Controls.Add(fields);
        panel.Controls.Add(title);
        buttons.Controls.Add(newEntry);
        return new EntryInputs
        {
            Panel = panel,
            FixedType = fixedType,
            Category = category,
            KindSelector = kind,
            Account = account,
            TransferTo = transferTo,
            TransferArrow = transferArrow,
            Description = description,
            Amount = amount,
            Date = date,
            BudgetMonth = budgetMonth,
            Recurring = recurring,
            Add = add,
            Save = save,
            Delete = delete,
            New = newEntry
        };
    }

    private static DataGridView CreateLedgerGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Surface,
            BorderStyle = BorderStyle.None,
            ForeColor = Background,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 38,
            RowTemplate = { Height = 34 }
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(51, 65, 95);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(191, 219, 254);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(237, 242, 255);
        RebuildLedger(grid, false);
        return grid;
    }

    private static void RebuildLedger(DataGridView grid, bool yearly)
    {
        grid.Columns.Clear();
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", Visible = false });
        if (yearly)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Month", HeaderText = "MONTH", DataPropertyName = "Month", FillWeight = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Income", HeaderText = "INCOME", DataPropertyName = "Income", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expenses", HeaderText = "SPENDING", DataPropertyName = "Expenses", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Savings", HeaderText = "SAVED", DataPropertyName = "Savings", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remaining", HeaderText = "LEFT", DataPropertyName = "Remaining", FillWeight = 100 });
        }
        else
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "DATE", DataPropertyName = "Date", FillWeight = 85 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "TYPE", DataPropertyName = "Type", FillWeight = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "CATEGORY", DataPropertyName = "Category", FillWeight = 95 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Account", HeaderText = "BANK ACCOUNT", DataPropertyName = "Account", FillWeight = 105 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", HeaderText = "DESCRIPTION", DataPropertyName = "Description", FillWeight = 120 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "AMOUNT", DataPropertyName = "Amount", FillWeight = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recurring", HeaderText = "REPEATS", DataPropertyName = "Recurring", FillWeight = 65 });
        }
    }

    private static List<object> BuildMonthRows(List<Dictionary<string, string>> records) =>
        records.Select(record => (object)new
        {
            Id = record["Id"],
            Date = DateTime.ParseExact(record["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd MMM")
                + (record["Date"].Substring(0, 7) == record["Month"] ? "" : $" → {DateTime.ParseExact(record["Month"] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture):MMM}"),
            Type = record["Type"] switch
            {
                "INCOME" => "Income +",
                "SAVINGS" => "Invested",
                "TRANSFER" => "Transfer",
                _ => "Expense −"
            },
            Category = record["Category"],
            Account = AccountText(record["Account"], record["TransferTo"]),
            Description = record["Description"],
            Amount = record["Type"] == "TRANSFER"
                ? $"↔ ₹{ReadAmount(record):N2}"
                : $"{(record["Type"] == "INCOME" ? "+" : "−")}₹{ReadAmount(record):N2}",
            Recurring = record["Recurring"]
        }).ToList();

    private static List<object> BuildYearRows(List<Dictionary<string, string>> records, int year)
    {
        var result = new List<object>();
        for (var month = 1; month <= 12; month++)
        {
            var monthKey = $"{year:0000}-{month:00}";
            var items = records.Where(r => r["Month"] == monthKey).ToList();
            decimal income = items.Where(r => r["Type"] == "INCOME").Sum(ReadAmount);
            decimal expenses = items.Where(r => r["Type"] == "EXPENSE").Sum(ReadAmount);
            decimal savings = items.Where(r => r["Type"] == "SAVINGS").Sum(ReadAmount);
            result.Add(new
            {
                Id = monthKey,
                Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month),
                Income = $"₹{income:N2}",
                Expenses = $"₹{expenses:N2}",
                Savings = $"₹{savings:N2}",
                Remaining = $"₹{income - expenses - savings:N2}"
            });
        }
        return result;
    }

    private static decimal ReadAmount(Dictionary<string, string> record) =>
        decimal.Parse(record["Amount"], CultureInfo.InvariantCulture);

    private Control BuildSpendingPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = Surface };
        var title = new Label
        {
            Text = "Spending by category",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        var chart = new Panel { Dock = DockStyle.Fill, BackColor = Surface, AutoScroll = true };
        panel.Controls.Add(chart);
        panel.Controls.Add(title);
        panel.Tag = chart;
        return panel;
    }

    private static void DrawSpendingBreakdown(Panel chart, List<Dictionary<string, string>> rows, string periodText)
    {
        chart.Controls.Clear();
        var groups = rows.Where(r => r["Type"] == "EXPENSE")
            .GroupBy(r => r["Category"])
            .Select(g => new { Category = g.Key, Amount = g.Sum(ReadAmount) })
            .OrderByDescending(g => g.Amount)
            .ToList();
        if (groups.Count == 0)
        {
            chart.Controls.Add(new Label
            {
                Text = $"No spending recorded {periodText} yet.",
                ForeColor = Muted,
                AutoSize = true,
                Location = new Point(4, 10)
            });
            return;
        }

        var maximum = groups.Max(g => g.Amount);
        var top = 6;
        foreach (var group in groups)
        {
            var name = new Label { Text = group.Category, ForeColor = Color.White, Location = new Point(4, top), Size = new Size(155, 22), AutoEllipsis = true };
            var amount = new Label
            {
                Text = $"₹{group.Amount:N0}",
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(160, chart.ClientSize.Width - 115), top),
                Size = new Size(105, 22)
            };
            var bar = new Panel
            {
                BackColor = Color.FromArgb(51, 65, 95),
                Location = new Point(4, top + 24),
                Size = new Size(Math.Max(120, chart.ClientSize.Width - 20), 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            bar.Controls.Add(new Panel
            {
                BackColor = Red,
                Dock = DockStyle.Left,
                Width = Math.Max(2, (int)(bar.Width * group.Amount / maximum))
            });
            chart.Controls.Add(name);
            chart.Controls.Add(amount);
            chart.Controls.Add(bar);
            top += 44;
        }
    }

    private static bool TryReadEntry(EntryInputs inputs, out string type, out string category,
        out string description, out decimal amount, out string account, out string transferTo)
    {
        type = inputs.Type;
        category = inputs.Category.Text.Trim();
        description = inputs.Description.Text.Trim();
        amount = inputs.Amount.Value;
        account = GetAccountValue(inputs.Account);
        transferTo = type == "TRANSFER" ? GetAccountValue(inputs.TransferTo) : "";
        if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(description) || amount <= 0)
        {
            MessageBox.Show("Enter a category, note, and amount greater than zero.",
                "Incomplete transaction", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        if (type == "TRANSFER" &&
            (account == "UNASSIGNED" || account == transferTo))
        {
            MessageBox.Show("Choose two different savings accounts for this transfer.",
                "Invalid account transfer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        return true;
    }

    private static string GetAccountValue(ComboBox selector) =>
        selector.SelectedItem is BankAccountOption option ? option.Value : "UNASSIGNED";

    private static void SelectAccount(ComboBox selector, string value)
    {
        selector.SelectedItem = selector.Items.Cast<BankAccountOption>()
            .FirstOrDefault(option => option.Value == value);
    }

    private static void UpdateAccountControls(EntryInputs input)
    {
        var isTransfer = input.Type == "TRANSFER";
        input.TransferArrow.Visible = isTransfer;
        input.TransferTo.Visible = isTransfer;
    }

    private static string AccountText(string source, string destination)
    {
        static string Label(string value) => value switch
        {
            "BANK1" => "Savings 1",
            "BANK2" => "Savings 2",
            _ => "Unassigned"
        };
        return destination.Length == 0 ? Label(source) : $"{Label(source)} → {Label(destination)}";
    }

    private static string GetAddLabel(EntryInputs input, bool isNew)
    {
        var typeName = input.Type switch
        {
            "INCOME" => "income",
            "SAVINGS" => "investment",
            "TRANSFER" => "transfer",
            _ => "spending"
        };
        return isNew ? $"Add another {typeName}" : $"Add {typeName}";
    }

    private static void AddField(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private static SummaryCard CreateCard(string title, Color valueColor)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(13), BackColor = Surface };
        var heading = new Label { Text = title, Dock = DockStyle.Top, Height = 24, ForeColor = Muted, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), AutoEllipsis = true };
        var value = new Label { Text = "₹0.00", Dock = DockStyle.Fill, ForeColor = valueColor, Font = new Font("Segoe UI", 17, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        panel.Controls.Add(value);
        panel.Controls.Add(heading);
        return new SummaryCard(panel, heading, value);
    }

    private static void SetCard(SummaryCard card, string title, string value)
    {
        card.Title.Text = title;
        card.Value.Text = value;
    }

    private static Button MakeButton(string text, Color color) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 32,
        Margin = new Padding(4, 0, 4, 0),
        BackColor = color,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private sealed class CategoryManagerDialog : Form
    {
        private readonly Database _database;
        private readonly ComboBox _type = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ListBox _categories = new() { Dock = DockStyle.Fill };
        private readonly TextBox _name = new() { Dock = DockStyle.Top, PlaceholderText = "Category name" };

        public CategoryManagerDialog(Database database)
        {
            _database = database;
            Text = "Manage categories";
            Size = new Size(480, 520);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Background;
            ForeColor = Color.White;
            _type.Items.AddRange(["EXPENSE", "INCOME", "SAVINGS"]);
            _type.SelectedIndex = 0;
            var add = MakeButton("Add", Green);
            var rename = MakeButton("Rename selected", Color.FromArgb(79, 70, 229));
            var delete = MakeButton("Delete selected", Color.FromArgb(190, 24, 93));
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, WrapContents = false };
            buttons.Controls.AddRange([add, rename, delete]);
            _categories.BackColor = Surface;
            _categories.ForeColor = Color.White;
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
            content.Controls.Add(_categories);
            content.Controls.Add(_name);
            content.Controls.Add(_type);
            Controls.Add(content);
            Controls.Add(buttons);

            void Refresh()
            {
                _categories.Items.Clear();
                _categories.Items.AddRange(_database.GetCategories(_type.Text).Cast<object>().ToArray());
            }
            _type.SelectedIndexChanged += (_, _) => Refresh();
            add.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_name.Text)) return;
                try
                {
                    _database.AddCategory(_type.Text, _name.Text.Trim());
                    _name.Clear();
                    Refresh();
                }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            rename.Click += (_, _) =>
            {
                if (_categories.SelectedItem is not string oldName || string.IsNullOrWhiteSpace(_name.Text)) return;
                try
                {
                    _database.RenameCategory(_type.Text, oldName, _name.Text.Trim());
                    _name.Clear();
                    Refresh();
                }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            delete.Click += (_, _) =>
            {
                if (_categories.SelectedItem is not string name) return;
                try
                {
                    _database.DeleteCategory(_type.Text, name);
                    Refresh();
                }
                catch (Exception ex) { ShowError(ex.Message); }
            };
            _categories.SelectedIndexChanged += (_, _) =>
            {
                if (_categories.SelectedItem is string name) _name.Text = name;
            };
            Refresh();
        }

        private void ShowError(string message) =>
            MessageBox.Show(this, message, "Unable to update category", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private sealed class TransactionMatchDialog : Form
    {
        private readonly ListBox _matches = new() { Dock = DockStyle.Fill };

        public TransactionMatchDialog(List<Dictionary<string, string>> records)
        {
            Text = "Choose matching transaction";
            Size = new Size(620, 370);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Background;
            foreach (var record in records)
            {
                var date = DateTime.ParseExact(record["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var month = DateTime.ParseExact(record["Month"] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture);
                _matches.Items.Add(new TransactionChoice(
                    long.Parse(record["Id"], CultureInfo.InvariantCulture),
                    $"{date:dd MMM yyyy} — {record["Category"]}: {record["Description"]} · ₹{ReadAmount(record):N2} ({month:MMMM yyyy})"));
            }

            var choose = MakeButton("Use selected transaction", Green);
            var cancel = MakeButton("Cancel", Color.FromArgb(51, 65, 95));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft };
            actions.Controls.AddRange([cancel, choose]);
            var hint = new Label
            {
                Text = "Several entries match this date and category. Select the one to increase.",
                Dock = DockStyle.Top,
                Height = 42,
                ForeColor = Muted,
                TextAlign = ContentAlignment.MiddleLeft
            };
            Controls.Add(_matches);
            Controls.Add(actions);
            Controls.Add(hint);
            choose.Click += (_, _) =>
            {
                if (_matches.SelectedItem is not TransactionChoice choice)
                {
                    MessageBox.Show(this, "Select a transaction first.", "Selection required",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                SelectedId = choice.Id;
                DialogResult = DialogResult.OK;
                Close();
            };
            cancel.Click += (_, _) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };
            _matches.DoubleClick += (_, _) => choose.PerformClick();
            AcceptButton = choose;
            CancelButton = cancel;
        }

        public long? SelectedId { get; private set; }

        private sealed record TransactionChoice(long Id, string Display)
        {
            public override string ToString() => Display;
        }
    }

    private sealed class OpeningBalancesDialog : Form
    {
        private readonly Database _database;
        private readonly NumericUpDown _bankOne = CreateAmountBox();
        private readonly NumericUpDown _bankTwo = CreateAmountBox();
        private readonly NumericUpDown _investments = CreateAmountBox();
        private readonly CalendarPicker _asOfDate = new("dd MMM yyyy") { Dock = DockStyle.Fill };

        public OpeningBalancesDialog(Database database)
        {
            _database = database;
            Text = "Starting balances";
            Size = new Size(540, 410);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Background;
            var starting = _database.GetOpeningBalances();
            _bankOne.Value = starting.BankOne;
            _bankTwo.Value = starting.BankTwo;
            _investments.Value = starting.Investments;
            _asOfDate.Value = starting.AsOfDate;
            var fields = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(22),
                ColumnCount = 2,
                RowCount = 6,
                BackColor = Background
            };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            AddField(fields, 0, "Savings account 1 (₹)", _bankOne);
            AddField(fields, 1, "Savings account 2 (₹)", _bankTwo);
            AddField(fields, 2, "Already invested — total (₹)", _investments);
            AddField(fields, 3, "Balance as of", _asOfDate);
            fields.Controls.Add(new Label
            {
                Text = "Enter actual balances for this snapshot. Earlier transactions stay in monthly totals but are not deducted again. Saving records the snapshot time; new same-day and future transactions then adjust the estimate.",
                ForeColor = Muted,
                Dock = DockStyle.Fill,
                AutoSize = false
            }, 0, 4);
            fields.SetColumnSpan(fields.GetControlFromPosition(0, 4)!, 2);
            var save = MakeButton("Save starting balances", Green);
            save.Click += (_, _) =>
            {
                _database.SaveOpeningBalances(_bankOne.Value, _bankTwo.Value, _investments.Value, _asOfDate.Value.Date);
                DialogResult = DialogResult.OK;
                Close();
            };
            fields.Controls.Add(save, 0, 5);
            fields.SetColumnSpan(save, 2);
            Controls.Add(fields);
        }

        private static NumericUpDown CreateAmountBox() => new()
        {
            Dock = DockStyle.Fill,
            DecimalPlaces = 2,
            Maximum = 999999999999,
            ThousandsSeparator = true
        };
    }

    private sealed class FinanceTrendChart : Panel
    {
        private readonly string[] _months = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames
            .Take(12).ToArray();
        private string[] _labels = [];
        private (string Name, Color Color, decimal[] Values)[] _series = [];
        private string _title = "";

        public FinanceTrendChart()
        {
            DoubleBuffered = true;
            BackColor = Surface;
            Padding = new Padding(12);
            ResizeRedraw = true;
        }

        public void SetMonthData(DateTime month, List<Dictionary<string, string>> records)
        {
            var days = DateTime.DaysInMonth(month.Year, month.Month);
            var daily = new decimal[3][];
            for (var series = 0; series < daily.Length; series++)
                daily[series] = new decimal[days];

            foreach (var record in records)
            {
                var date = DateTime.ParseExact(record["Date"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var day = date.Year == month.Year && date.Month == month.Month ? date.Day - 1 : 0;
                var series = record["Type"] switch
                {
                    "INCOME" => 0,
                    "EXPENSE" => 1,
                    "SAVINGS" => 2,
                    _ => -1
                };
                if (series >= 0) daily[series][day] += ReadAmount(record);
            }

            var cumulative = new decimal[4][];
            for (var series = 0; series < cumulative.Length; series++)
                cumulative[series] = new decimal[days];
            decimal income = 0, spending = 0, saved = 0;
            for (var day = 0; day < days; day++)
            {
                income += daily[0][day];
                spending += daily[1][day];
                saved += daily[2][day];
                cumulative[0][day] = income;
                cumulative[1][day] = spending;
                cumulative[2][day] = saved;
                cumulative[3][day] = income - spending - saved;
            }

            SetChartData(Enumerable.Range(1, days).Select(day => day.ToString(CultureInfo.InvariantCulture)).ToArray(),
                cumulative, $"Cumulative daily totals — {month:MMMM yyyy}");
        }

        public void SetYearData(int year, List<Dictionary<string, string>> records)
        {
            var monthly = Enumerable.Range(1, 12).Select(month =>
            {
                var key = $"{year:0000}-{month:00}";
                var items = records.Where(record => record["Month"] == key);
                var income = items.Where(record => record["Type"] == "INCOME").Sum(ReadAmount);
                var spending = items.Where(record => record["Type"] == "EXPENSE").Sum(ReadAmount);
                var saved = items.Where(record => record["Type"] == "SAVINGS").Sum(ReadAmount);
                return new[] { income, spending, saved, income - spending - saved };
            }).ToArray();
            var values = Enumerable.Range(0, 4)
                .Select(series => monthly.Select(month => month[series]).ToArray())
                .ToArray();
            SetChartData(_months, values, $"Monthly totals — {year}");
        }

        private void SetChartData(string[] labels, decimal[][] values, string title)
        {
            _labels = labels;
            _title = title;
            _series =
            [
                ("Income", Green, values[0]),
                ("Spending", Red, values[1]),
                ("Saved", Blue, values[2]),
                ("Left after spending", Purple, values[3])
            ];
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Surface);
            if (ClientSize.Width < 280 || ClientSize.Height < 220 || _labels.Length == 0) return;

            var allValues = _series.SelectMany(series => series.Values).ToArray();
            var min = Math.Min(0, allValues.Min());
            var max = Math.Max(1, allValues.Max());
            var plot = new RectangleF(86, 96, ClientSize.Width - 122, ClientSize.Height - 158);
            if (plot.Width <= 0 || plot.Height <= 0) return;

            using var gridPen = new Pen(Color.FromArgb(70, 91, 128), 1);
            using var axisFont = new Font("Segoe UI", 9);
            using var legendFont = new Font("Segoe UI", 9, FontStyle.Bold);
            using var headingFont = new Font("Segoe UI", 13, FontStyle.Bold);
            using var labelBrush = new SolidBrush(Muted);
            using var valueBrush = new SolidBrush(Color.White);
            for (var line = 0; line <= 4; line++)
            {
                var fraction = line / 4f;
                var y = plot.Bottom - plot.Height * fraction;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                var value = min + (decimal)fraction * (max - min);
                graphics.DrawString(FormatAxis(value), axisFont, labelBrush, 8, y - 9);
            }

            var labelStep = _labels.Length <= 12 ? 1 : 5;
            for (var index = 0; index < _labels.Length; index++)
            {
                if (index != 0 && index != _labels.Length - 1 && (index + 1) % labelStep != 0) continue;
                var x = plot.Left + plot.Width * index / Math.Max(1, _labels.Length - 1);
                graphics.DrawString(_labels[index], axisFont, labelBrush, x - 10, plot.Bottom + 8);
            }

            var legendX = plot.Left;
            foreach (var item in _series)
            {
                using var linePen = new Pen(item.Color, 3);
                graphics.DrawLine(linePen, legendX, 28, legendX + 24, 28);
                graphics.DrawString(item.Name, legendFont, valueBrush, legendX + 30, 20);
                legendX += item.Name.Length * 8 + 65;
            }
            graphics.DrawString(_title, headingFont, valueBrush, plot.Left, 55);

            foreach (var item in _series)
            {
                using var linePen = new Pen(item.Color, 3.5f);
                using var pointBrush = new SolidBrush(item.Color);
                PointF? previous = null;
                for (var month = 0; month < item.Values.Length; month++)
                {
                    var x = plot.Left + plot.Width * month / Math.Max(1, item.Values.Length - 1);
                    var fraction = (float)((item.Values[month] - min) / (max - min));
                    var point = new PointF(x, plot.Bottom - plot.Height * fraction);
                    if (previous is { } prior) graphics.DrawLine(linePen, prior, point);
                    graphics.FillEllipse(pointBrush, point.X - 4, point.Y - 4, 8, 8);
                    previous = point;
                }
            }
        }

        private static string FormatAxis(decimal amount) =>
            amount >= 100000 ? $"₹{amount / 100000m:N0}L" :
            amount >= 1000 ? $"₹{amount / 1000m:N0}k" :
            $"₹{amount:N0}";
    }
}
