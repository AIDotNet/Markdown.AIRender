using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Styling;

using AvaloniaXmlTranslator;
using AvaloniaXmlTranslator.Models;

using MarkdownAIRender.Controls.MarkdownRender;

namespace SamplesMarkdown.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        InitSampleFiles();
        InitLanguage();
        InitMarkdownThemes();
    }

    #region Properties

    public List<string> MarkdownFiles { get; private set; }

    private string? _selectedFile;

    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            SetProperty(ref _selectedFile, value);
            ReadMarkdown();
        }
    }

    private string _markdown;

    public string Markdown
    {
        get => _markdown;
        set => this.SetProperty(ref _markdown, value);
    }

    public ObservableCollection<MarkdownTheme> MarkdownThemes { get; private set; }

    private MarkdownTheme? _selectedMarkdownTheme;

    public MarkdownTheme? SelectedMarkdownTheme
    {
        get => _selectedMarkdownTheme;
        set
        {
            this.SetProperty(ref _selectedMarkdownTheme, value);
            MarkdownClass.ChangeTheme(_selectedMarkdownTheme.Key);
        }
    }


    public ObservableCollection<LocalizationLanguage> Languages { get; private set; }

    private LocalizationLanguage? _selectedLanguage;

    public LocalizationLanguage? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            this.SetProperty(ref _selectedLanguage, value);
            SetLanguage();
        }
    }

    #endregion

    #region Command handlers

    public async Task RaiseChangeThemeHandler()
    {
        App.Current.RequestedThemeVariant = App.Current.RequestedThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    #endregion

    #region private methods

    private void InitSampleFiles()
    {
        MarkdownFiles = Directory.GetFiles("markdowns", "*.md").Select(f=>new FileInfo(f).Name).ToList();
        SelectedFile = MarkdownFiles.FirstOrDefault();
    }

    private void InitLanguage()
    {
        var languages = I18nManager.Instance.GetLanguages();
        Languages = new ObservableCollection<LocalizationLanguage>(languages);

        var language = Thread.CurrentThread.CurrentCulture.Name;
        SelectedLanguage = Languages.FirstOrDefault(l => l.CultureName == language);
    }

    private void InitMarkdownThemes()
    {
        MarkdownThemes = new ObservableCollection<MarkdownTheme>(MarkdownClass.Themes);
        SelectedMarkdownTheme = MarkdownClass.Themes.FirstOrDefault(item => item.Key == MarkdownClass.CurrentThemeKey);
    }

    private void ReadMarkdown()
    {
        if (string.IsNullOrWhiteSpace(SelectedFile))
        {
            Markdown = "No markdown contents";
        }
        else
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "markdowns", SelectedFile);
            Markdown = File.ReadAllText(filePath);
        }
    }

    private void SetLanguage()
    {
        var culture = new CultureInfo(SelectedLanguage?.CultureName);
        I18nManager.Instance.Culture = culture;
    }

    #endregion
}