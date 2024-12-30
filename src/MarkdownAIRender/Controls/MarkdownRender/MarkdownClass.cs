using Avalonia;
using Avalonia.Controls;

namespace MarkdownAIRender.Controls.MarkdownRender;

public class MarkdownClass : AvaloniaObject
{
    static MarkdownClass()
    {
        TargetProperty.Changed.AddClassHandler<AvaloniaObject>(OnTargetPropertyChanged);
    }

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


    #region Attached Properties

    public static readonly AttachedProperty<Control> TargetProperty =
        AvaloniaProperty.RegisterAttached<MarkdownClass, Control, Control>("Target");

    public static void SetTarget(AvaloniaObject element, Control parameter)
    {
        element.SetValue(TargetProperty, parameter);
    }

    public static object GetTarget(AvaloniaObject element)
    {
        return element.GetValue(TargetProperty);
    }

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

    public static void AddControl(Control control)
    {
        var baseClass =
            control.Classes.FirstOrDefault(item => item.StartsWith(MarkdownClassPrefix) && !item.Contains("_"));
        _boundControls[control] = baseClass;
        ChangeTheme(control, baseClass);
    }

    #endregion

    #region Private Methods

    private static void OnTargetPropertyChanged(AvaloniaObject obj, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is Control oldControl)
        {
            RemoveControl(oldControl);
        }

        if (e.NewValue is Control newControl)
        {
            AddControl(newControl);
        }
    }

    #endregion
}

public record MarkdownTheme(string Name, string Key);