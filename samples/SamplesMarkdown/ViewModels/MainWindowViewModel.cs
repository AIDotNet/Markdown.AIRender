using System.Threading.Tasks;

using Avalonia.Styling;

namespace SamplesMarkdown.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string markdown;

    public string Markdown
    {
        get => markdown;
        set => this.SetProperty(ref markdown, value);
    }

    public async Task RaiseChangeThemeHandler()
    {
        App.Current.RequestedThemeVariant = App.Current.RequestedThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
}