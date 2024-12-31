using Avalonia.Controls;

namespace MarkdownAIRender.Controls.MarkdownRender;

public static class MarkdownClass
{
    private static readonly Dictionary<Control, string?> _boundControls = new();
    private static readonly string MarkdownClassPrefix = "Md";

    #region Public Properties

    public static List<MarkdownTheme> Themes { get; private set; } = new()
    {
        new("默认主题", ""),
        new("橙心", "OrangeHeart"),
        new("墨黑", "Inkiness"),
        new("姹紫", "ColorfulPurple"),
        new("科技蓝", "TechnologyBlue")
    };

    public static string CurrentThemeKey { get; private set; } = "";

    #endregion

    #region Public Methods

    public static void ChangeTheme(string themeName)
    {
        CurrentThemeKey = themeName;
        foreach (var control in _boundControls)
        {
            ChangeTheme(control.Key, control.Value);
        }
    }

    public static void ChangeTheme(Control control, string? baseClass)
    {
        if (string.IsNullOrWhiteSpace(baseClass))
        {
            return;
        }

        var currentFactThemeKey =
            string.IsNullOrWhiteSpace(CurrentThemeKey) ? baseClass : $"{baseClass}_{CurrentThemeKey}";
        if (control.Classes.Contains(currentFactThemeKey))
        {
            return;
        }

        var oldMdClasses = control.Classes.Where(name => name.StartsWith(MarkdownClassPrefix))
            .ToList();
        foreach (var oldMdClass in oldMdClasses)
        {
            control.Classes.Remove(oldMdClass);
        }

        control.Classes.Add(currentFactThemeKey);
    }

    public static void RemoveControl(Control control)
    {
        _boundControls.Remove(control);
    }

    public static void AddMdClass(this Control control, string className)
    {
        _boundControls[control] = className;
        ChangeTheme(control, className);
    }

    #endregion
}

public record MarkdownTheme(string Name, string Key);