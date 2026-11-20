namespace FinanceMobile;

internal sealed class LoginPage : ContentPage
{
    private readonly LocalFinanceStore _store;
    private readonly Entry _username = new() { Placeholder = "Username", ReturnType = ReturnType.Next, TextColor = Colors.White };
    private readonly Entry _password = new() { Placeholder = "Password (14+ characters)", IsPassword = true, ReturnType = ReturnType.Done, TextColor = Colors.White };
    private readonly Label _status = new() { TextColor = Color.FromArgb("#F87171"), FontSize = 14 };

    public LoginPage(LocalFinanceStore store)
    {
        _store = store;
        Title = "Vault Finance";
        BackgroundColor = Color.FromArgb("#0B1226");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(28, 72, 28, 28),
                Spacing = 18,
                Children =
                {
                    new Label
                    {
                        Text = "Vault Finance",
                        FontSize = 30,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.White
                    },
                    new Label
                    {
                        Text = "Your budget stays in this app on this device.",
                        FontSize = 16,
                        TextColor = Color.FromArgb("#A6B5CF")
                    },
                    _username,
                    _password,
                    MakeButton("Create local account", "#10B981", () => RunAccountAction(create: true)),
                    MakeButton("Unlock my account", "#4F46E5", () => RunAccountAction(create: false)),
                    _status,
                    new Label
                    {
                        Text = "Create a local username and password the first time. They are not sent to a server. Remember your password; there is no online recovery.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#A6B5CF")
                    }
                }
            }
        };
        _username.AutomationId = "UsernameEntry";
        _password.AutomationId = "PasswordEntry";
    }

    private async void RunAccountAction(bool create)
    {
        _status.Text = "";
        try
        {
            var user = create
                ? _store.CreateAccount(_username.Text ?? "", _password.Text ?? "")
                : _store.Authenticate(_username.Text ?? "", _password.Text ?? "");
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window is null)
                throw new InvalidOperationException("The app window is not available.");
            window.Page = new MainPage(_store, user);
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            await DisplayAlertAsync(create ? "Could not create account" : "Could not unlock account", ex.Message, "OK");
        }
    }

    private static Button MakeButton(string text, string color, Action action)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb(color),
            TextColor = Colors.White,
            CornerRadius = 12,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold
        };
        button.Clicked += (_, _) => action();
        return button;
    }
}
