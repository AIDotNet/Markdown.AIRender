using System;

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
            vm.Markdown = @"
# Hello, Avalonia!
```csharp
public class Test{
    
}
```
## Welcome to Avalonia!

This is a simple markdown editor built using [Avalonia](https://avaloniaui.net/).

功能列表
- [x] 语法高亮
- [x] 代码块

";
        }
    }
}