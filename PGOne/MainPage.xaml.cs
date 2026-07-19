namespace PGOne;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void OnBlazorWebViewInitialized(object? sender, EventArgs e)
    {
        statusBanner.IsVisible = false;
        errorPanel.IsVisible = false;
    }

    public void ShowWebViewError(string message)
    {
        errorMessage.Text = message;
        errorPanel.IsVisible = true;
    }
}
