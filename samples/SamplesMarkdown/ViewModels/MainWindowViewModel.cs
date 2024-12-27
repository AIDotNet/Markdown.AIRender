namespace SamplesMarkdown.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private string markdown;

    public string Markdown
    {
        get => markdown;
        set => this.SetProperty(ref markdown, value);
    }
}