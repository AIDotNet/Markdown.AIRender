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

    private static readonly List<Control> _boundControls = new();
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
        foreach (Control control in _boundControls)
        {
            ChangeTheme(control);
        }
    }

    public static void ChangeTheme(Control control)
    {
        var oldClasses = control.Classes.ToList();
        if (oldClasses == null)
        {
            return;
        }
        var newClasses = new List<string>();
        var baseMdClass =
            oldClasses.FirstOrDefault(name => name.StartsWith(MarkdownClassPrefix) && !name.Contains("_"));
        var newUpdateMdClass = $"{baseMdClass}_{CurrentTheme}";
        if (string.IsNullOrWhiteSpace(baseMdClass))
        {
            return;
        }
        newClasses.Add(baseMdClass);
        newClasses.Add(newUpdateMdClass);

        control.Classes.Replace(newClasses);
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
            _boundControls.Add(newControl);
            ChangeTheme(newControl);
        }
    }

    #endregion
}