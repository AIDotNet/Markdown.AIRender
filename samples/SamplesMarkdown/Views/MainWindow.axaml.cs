using System;
using System.IO;
using System.Linq;

using Avalonia.Controls;

using SamplesMarkdown.ViewModels;

namespace SamplesMarkdown.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            var mdFile = Directory.GetFiles("markdowns", "*.md").FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(mdFile))
            {
                vm.Markdown = File.ReadAllText(mdFile);
            }
        }
    }
}