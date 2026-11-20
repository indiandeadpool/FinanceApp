namespace FinanceMobile;

public partial class App : Application
{
	private readonly LocalFinanceStore _store = new();

	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new LoginPage(_store));
	}
}