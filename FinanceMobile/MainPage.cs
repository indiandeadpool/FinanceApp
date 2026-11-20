using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace FinanceMobile;

internal sealed class MainPage : ContentPage
{
    private static readonly Color PageBackground = Color.FromArgb("#0B1226");
    private static readonly Color Surface = Color.FromArgb("#1B2643");
    private static readonly Color Muted = Color.FromArgb("#A6B5CF");
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("en-IN");

    private readonly LocalFinanceStore _store;
    private readonly string _username;
    private readonly DatePicker _monthPicker = new() { Date = DateTime.Today, Format = "MMMM yyyy" };
    private readonly DatePicker _entryDate = new() { Date = DateTime.Today, Format = "dd MMM yyyy" };
    private readonly DatePicker _appliedMonth = new() { Date = DateTime.Today, Format = "MMMM yyyy" };
    private readonly Picker _type = new() { Title = "Income or expense" };
    private readonly Picker _account = new() { Title = "Savings account" };
    private readonly Entry _amount = InputEntry("Amount (₹)", Keyboard.Numeric);
    private readonly Entry _category = InputEntry("Category (e.g. Food, Salary)");
    private readonly Entry _note = InputEntry("Note");
    private readonly Entry _startingOne = InputEntry("Account 1 balance", Keyboard.Numeric);
    private readonly Entry _startingTwo = InputEntry("Account 2 balance", Keyboard.Numeric);
    private readonly DatePicker _startingDate = new() { Date = DateTime.Today, Format = "dd MMM yyyy" };
    private readonly VerticalStackLayout _startingPanel = new() { Spacing = 10, IsVisible = false };
    private readonly VerticalStackLayout _transactionList = new() { Spacing = 8 };
    private readonly Label _incomeValue = ValueLabel();
    private readonly Label _spendingValue = ValueLabel();
    private readonly Label _leftValue = ValueLabel();
    private readonly Label _accountOneValue = ValueLabel();
    private readonly Label _accountTwoValue = ValueLabel();

    public MainPage(LocalFinanceStore store, string username)
    {
        _store = store;
        _username = username;
        Title = "Vault Finance";
        BackgroundColor = PageBackground;
        _type.Items.Add("Expense");
        _type.Items.Add("Income");
        _type.SelectedIndex = 0;
        _account.Items.Add("Savings account 1");
        _account.Items.Add("Savings account 2");
        _account.SelectedIndex = 0;
        _monthPicker.DateSelected += (_, _) => RefreshDashboard();

        var title = new Label
        {
            Text = "Money overview",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };
        var welcome = new Label { Text = $"Local profile: {_username}", TextColor = Muted, FontSize = 14 };
        var monthLabel = SectionLabel("MONTHLY OVERVIEW");
        var incomeCard = MetricCard("Income", _incomeValue, "#10B981");
        var spendingCard = MetricCard("Spending", _spendingValue, "#F45370");
        var leftCard = MetricCard("Left", _leftValue, "#38BDF8");
        var metrics = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        metrics.Add(incomeCard, 0, 0);
        metrics.Add(spendingCard, 1, 0);
        metrics.Add(leftCard, 2, 0);

        var balances = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        balances.Add(MetricCard("Savings account 1 · now", _accountOneValue, "#A78BFA"), 0, 0);
        balances.Add(MetricCard("Savings account 2 · now", _accountTwoValue, "#38BDF8"), 1, 0);

        _startingPanel.Children.Add(FieldLabel("Savings account 1 balance (₹)"));
        _startingPanel.Children.Add(_startingOne);
        _startingPanel.Children.Add(FieldLabel("Savings account 2 balance (₹)"));
        _startingPanel.Children.Add(_startingTwo);
        _startingPanel.Children.Add(FieldLabel("Balances as of"));
        _startingPanel.Children.Add(_startingDate);
        _startingPanel.Children.Add(MakeButton("Save starting balances", "#7C3AED", SaveStartingBalances));

        var form = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                SectionLabel("ADD INCOME OR EXPENSE"),
                _type,
                _category,
                _account,
                FieldLabel("Transaction date"),
                _entryDate,
                FieldLabel("Applies to month"),
                _appliedMonth,
                _amount,
                _note,
                MakeButton("Add transaction", "#10B981", AddTransaction)
            }
        };
        var transactionHeading = SectionLabel("TRANSACTIONS");
        var transactionHint = new Label
        {
            Text = "Monthly totals follow “Applies to month.” Current account balances follow transaction date and the selected bank account. Future-dated entries do not reduce today’s balance.",
            TextColor = Muted,
            FontSize = 13
        };
        var logout = MakeButton("Lock vault", "#334155", LockVault);
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(18, 30, 18, 28),
            Spacing = 14,
            Children =
            {
                title,
                welcome,
                SectionLabel("CURRENT BANK BALANCES"),
                balances,
                MakeButton("Starting balances", "#4F46E5", ToggleStartingPanel),
                _startingPanel,
                monthLabel,
                _monthPicker,
                metrics,
                form,
                transactionHeading,
                transactionHint,
                _transactionList,
                logout
            }
        };
        Content = new ScrollView { Content = content };
        _startingOne.AutomationId = "StartingAccountOne";
        _startingTwo.AutomationId = "StartingAccountTwo";
        _amount.AutomationId = "TransactionAmount";
        RefreshDashboard();
    }

    private async void AddTransaction()
    {
        if (!TryParseAmount(_amount.Text, out var amount) || amount <= 0)
        {
            await DisplayAlertAsync("Invalid amount", "Enter an amount greater than zero.", "OK");
            return;
        }

        var type = _type.SelectedIndex == 1 ? "INCOME" : "EXPENSE";
        var category = string.IsNullOrWhiteSpace(_category.Text)
            ? type == "INCOME" ? "Other Income" : "Other"
            : _category.Text.Trim();
        var description = string.IsNullOrWhiteSpace(_note.Text) ? category : _note.Text.Trim();
        var account = _account.SelectedIndex == 1 ? "BANK2" : "BANK1";
        var appliedDate = _appliedMonth.Date ?? DateTime.Today;
        var appliedMonth = new DateTime(appliedDate.Year, appliedDate.Month, 1)
            .ToString("yyyy-MM", CultureInfo.InvariantCulture);

        try
        {
            _store.AddTransaction(amount, type, category, description,
                _entryDate.Date ?? DateTime.Today, appliedMonth, account);
            _amount.Text = "";
            _category.Text = "";
            _note.Text = "";
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Could not add transaction", ex.Message, "OK");
        }
    }

    private async void SaveStartingBalances()
    {
        if (!TryParseAmount(_startingOne.Text, out var accountOne) ||
            !TryParseAmount(_startingTwo.Text, out var accountTwo) ||
            accountOne < 0 || accountTwo < 0)
        {
            await DisplayAlertAsync("Invalid balances", "Enter zero or a positive amount for both accounts.", "OK");
            return;
        }

        try
        {
            _store.SaveStartingBalances(accountOne, accountTwo, _startingDate.Date ?? DateTime.Today);
            _startingPanel.IsVisible = false;
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Could not save balances", ex.Message, "OK");
        }
    }

    private void ToggleStartingPanel()
    {
        var isOpening = !_startingPanel.IsVisible;
        if (isOpening)
        {
            var balances = _store.GetStartingBalances();
            _startingOne.Text = balances.AccountOne.ToString("0.00", CultureInfo.InvariantCulture);
            _startingTwo.Text = balances.AccountTwo.ToString("0.00", CultureInfo.InvariantCulture);
            _startingDate.Date = balances.AsOfDate;
        }
        _startingPanel.IsVisible = isOpening;
    }

    private async void DeleteTransaction(long id)
    {
        if (!await DisplayAlertAsync("Delete transaction?", "This transaction will be removed from your local ledger.", "Delete", "Cancel"))
            return;
        _store.DeleteTransaction(id);
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        var selectedMonth = _monthPicker.Date ?? DateTime.Today;
        var monthKey = $"{selectedMonth.Year:0000}-{selectedMonth.Month:00}";
        var transactions = _store.GetTransactions(monthKey);
        var income = transactions.Where(item => item.Type == "INCOME").Sum(item => item.Amount);
        var spending = transactions.Where(item => item.Type == "EXPENSE").Sum(item => item.Amount);
        var balances = _store.GetCurrentBalances();
        _incomeValue.Text = FormatCurrency(income);
        _spendingValue.Text = FormatCurrency(spending);
        _leftValue.Text = FormatCurrency(income - spending);
        _accountOneValue.Text = FormatCurrency(balances.AccountOne);
        _accountTwoValue.Text = FormatCurrency(balances.AccountTwo);
        _transactionList.Children.Clear();
        if (transactions.Count == 0)
        {
            _transactionList.Children.Add(new Label
            {
                Text = "No transactions for this month yet.",
                TextColor = Muted,
                Margin = new Thickness(2, 4)
            });
            return;
        }

        foreach (var item in transactions)
        {
            var amountColor = item.Type == "INCOME" ? "#10B981" : "#F45370";
            var details = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = $"{item.Category} · {FormatCurrency(item.Amount)}", TextColor = Color.FromArgb(amountColor), FontAttributes = FontAttributes.Bold },
                    new Label { Text = $"{item.EntryDate:dd MMM yyyy} · {AccountName(item.Account)} · {item.Description}", TextColor = Muted, FontSize = 12 }
                }
            };
            var delete = new Button
            {
                Text = "Delete",
                TextColor = Colors.White,
                BackgroundColor = Color.FromArgb("#7F1D1D"),
                CornerRadius = 8,
                Padding = new Thickness(8, 2)
            };
            delete.Clicked += (_, _) => DeleteTransaction(item.Id);
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                Children = { details, delete }
            };
            Grid.SetColumn(delete, 1);
            _transactionList.Children.Add(new Border
            {
                BackgroundColor = Surface,
                Stroke = Color.FromArgb("#33415F"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = 12,
                Content = row
            });
        }
    }

    private void LockVault()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is not null) window.Page = new LoginPage(_store);
    }

    private static string AccountName(string account) =>
        account == "BANK2" ? "Savings account 2" : "Savings account 1";

    private static bool TryParseAmount(string? text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out amount) ||
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private static string FormatCurrency(decimal value) =>
        $"₹{value.ToString("N2", CurrencyCulture)}";

    private static Label ValueLabel() => new()
    {
        Text = "₹0.00",
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White,
        LineBreakMode = LineBreakMode.TailTruncation
    };

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Muted,
        Margin = new Thickness(0, 8, 0, 0)
    };

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextColor = Muted
    };

    private static Entry InputEntry(string placeholder, Keyboard? keyboard = null) => new()
    {
        Placeholder = placeholder,
        Keyboard = keyboard ?? Keyboard.Default,
        TextColor = Colors.White,
        PlaceholderColor = Muted,
        BackgroundColor = Surface
    };

    private static Border MetricCard(string title, Label value, string accent) =>
        new()
        {
            BackgroundColor = Surface,
            Stroke = Color.FromArgb("#33415F"),
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = 10,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = title, FontSize = 11, TextColor = Color.FromArgb(accent), LineBreakMode = LineBreakMode.TailTruncation },
                    value
                }
            }
        };

    private static Button MakeButton(string text, string color, Action action)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb(color),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 48
        };
        button.Clicked += (_, _) => action();
        return button;
    }
}
