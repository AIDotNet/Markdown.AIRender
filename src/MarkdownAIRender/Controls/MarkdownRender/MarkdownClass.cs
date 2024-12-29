using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

using TextMateSharp.Grammars;

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

    public static List<string> Themes { get; private set; } = new List<string>() { "Inkiness", "OrangeHeart" };

    public static string CurrentTheme { get; private set; } = "Inkiness";

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
        CurrentTheme = themeName;
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

        if (!control.Classes.Contains(baseClass))
        {
            control.Classes.Add(baseClass);
        }
        var newUpdateMdClass = $"{baseClass}_{CurrentTheme}";
        if (control.Classes.Contains(newUpdateMdClass))
        {
            return;
        }

        var oldMdClasses = control.Classes.Where(name => name.StartsWith(MarkdownClassPrefix) && name.Contains("_"))
            .ToList();
        foreach (var oldMdClass in oldMdClasses)
        {
            control.Classes.Remove(oldMdClass);
        }

        control.Classes.Add(newUpdateMdClass);
    }

    #endregion

    #region Private Methods

    private static void OnTargetPropertyChanged(AvaloniaObject obj, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is Control oldControl)
        {
            _boundControls.Remove(oldControl);
        }

        if (e.NewValue is Control newControl)
        {
            var baseClass =
                newControl.Classes.FirstOrDefault(item => item.StartsWith(MarkdownClassPrefix) && !item.Contains("_"));
            _boundControls[newControl] = baseClass;
            ChangeTheme(newControl, baseClass);
        }
    }

    #endregion
}