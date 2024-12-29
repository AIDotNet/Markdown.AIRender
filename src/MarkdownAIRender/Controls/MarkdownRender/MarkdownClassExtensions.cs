using Avalonia;
using Avalonia.Controls;

namespace MarkdownAIRender.Controls.MarkdownRender;

public class MarkdownClassExtensions
{
    private static readonly List<Control> _boundControls = new();

    #region Public Properties

    public static List<string> Themes { get; private set; } = new List<string>() { "Dark", "Light" };

    #endregion
    
    #region Attached Properties
    
    public static AttachedProperty<Control> MdClassProperty = 
        AvaloniaProperty.RegisterAttached<MarkdownClassExtensions, Control, Control>("MdClass");
    public static void SetMdClass(AvaloniaObject obj, Control? value) => obj.SetValue(MdClassProperty, value);
    public static Control GetMdClass(AvaloniaObject obj) => obj.GetValue(MdClassProperty);
    
    #endregion

    #region Public Methods

    public static void ChangeTheme(string themeName)
    {
        foreach (Control control in _boundControls)
        {
            foreach (string theme in Themes)
            {
                control.Classes.Remove(theme);
            }
            control.Classes.Add(themeName);
        }
    }
    
    #endregion
    
    #region Private Methods
    
    private static void OnMdClassChanged(AvaloniaObject obj,  Control? oldValue,  Control? newValue)
    {
        if (oldValue != null)
        {
            _boundControls.Remove(oldValue);
        }

        if (newValue != null)
        {
            _boundControls.Add(newValue);
        }
    }
    
    #endregion
}