using TheBleedingDeacons.Intergroup.Monitor.ViewModels;

namespace TheBleedingDeacons.Intergroup.Monitor.Views;

public partial class SignInPage : ContentPage
{
	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();

		BindingContext = viewModel;
	}
}
