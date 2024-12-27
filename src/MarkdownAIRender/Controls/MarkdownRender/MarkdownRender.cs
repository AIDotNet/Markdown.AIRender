using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkdownAIRender.Controls.Images;
using MarkdownAIRender.Helper;

using TextMateSharp.Grammars;

using Inline = Avalonia.Controls.Documents.Inline;

namespace MarkdownAIRender.Controls.MarkdownRender;

public class MarkdownRender : ContentControl, INotifyPropertyChanged
{
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MarkdownRender, string?>(
        nameof(Value));


    public string? Value
    {
        get => GetValue(ValueProperty);
        set
        {
            SetValue(ValueProperty, value);
            Render(value);
        }
    }

    private string _copyText = "Copy";

    public string CopyText
    {
        get => _copyText;
        set
        {
            _copyText = value;
            OnPropertyChanged(nameof(CopyText));
        }
    }

    /// <summary>
    /// 复制按钮触发事件
    /// </summary>
    public event EventHandler CopyClick;

    public new event PropertyChangedEventHandler? PropertyChanged;
    private WindowNotificationManager _notificationManager;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Render(Value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Render(Value);

        Application.Current.ActualThemeVariantChanged += ThemeChanged;
        _notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(this))
        {
            Position = NotificationPosition.TopRight, MaxItems = 3, Margin = new Thickness(0, 0, 15, 40)
        };
    }

    private void ThemeChanged(object? sender, EventArgs e)
    {
        Render(Value);
    }

    /// <summary>
    /// 将 Markdown 文本解析并渲染为 Avalonia 控件，最终设置到本 UserControl 的 Content 中。
    /// </summary>
    /// <param name="markdown">要渲染的 Markdown 内容</param>
    public void Render(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return;
        }

        // 1. 使用 Markdig 解析 Markdown 文本
        var document = Markdown.Parse(markdown ?? "");

        // 2. 准备一个根容器，用于放置渲染结果
        var container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };

        // 3. 遍历文档中的所有 Block，分别转成 Avalonia 的控件
        foreach (var block in document)
        {
            var control = ConvertBlock(block);
            if (control != null)
            {
                container.Children.Add(control);
            }
        }

        Content = container;
    }

    /// <summary>
    /// 将 Markdig 的 Block 对象转换为对应的 Avalonia 控件
    /// </summary>
    private Control? ConvertBlock(Block block)
    {
        switch (block)
        {
            // ---- 段落 ----
            case ParagraphBlock paragraph:
                return CreateParagraph(paragraph);

            // ---- 标题 ----
            case HeadingBlock heading:
                return CreateHeading(heading);

            // ---- 代码块 ----
            case FencedCodeBlock codeBlock:
                return CreateCodeBlock(codeBlock);

            // ---- 列表 ----
            case ListBlock listBlock:
                return CreateList(listBlock);

            case QuoteBlock quoteBlock:
                return CreateQuote(quoteBlock);

            case ThematicBreakBlock _:
                return new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 5, 0, 5)
                };

            // 其它类型暂不处理，简单输出其文本
            default:
                return new TextBox
                {
                    IsReadOnly = true,
                    IsEnabled = true,
                    AcceptsReturn = true,
                    Classes = { "markdown" },
                    Margin = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderBrush = Brushes.Transparent,
                    TextWrapping = TextWrapping.WrapWithOverflow,
                    Text = block.ToString()
                };
        }
    }

    private Control CreateQuote(QuoteBlock quoteBlock)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 5, 5, 5),
            Margin = new Thickness(8, 5, 0, 5)
        };

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };

        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 5),
        };

        stackPanel.Children.Add(headerPanel);
        border.Child = stackPanel;

        foreach (Block block in quoteBlock)
        {
            var control = ConvertBlock(block);
            if (control != null)
            {
                stackPanel.Children.Add(control);
            }
        }

        return border;
    }

    /// <summary>
    /// 创建段落控件
    /// </summary>
    private Control CreateParagraph(ParagraphBlock paragraph)
    {
        var container = new WrapPanel { Orientation = Orientation.Horizontal };
        // 段落里包含一系列的 Inline (Markdig 类型)，需要转成 Avalonia 的 Inline
        int index = 0;
        if (paragraph.Inline != null)
        {
            var controls = ConvertInlineContainer(paragraph.Inline);
            foreach (var control in controls)
            {
                if (control is Inline inline)
                {
                    if (container.Children.Count > 0 && container.Children.LastOrDefault() is SelectableTextBlock span)
                    {
                        span.Inlines?.Add(inline);
                    }
                    else
                    {
                        span = new SelectableTextBlock { Inlines = new InlineCollection() };
                        span.Inlines?.Add(inline);
                        container.Children.Add(span);
                    }
                }
                else if (control is Control item)
                {
                    container.Children.Add(item);
                }
            }
        }

        return container;
    }

    /// <summary>
    /// 创建标题控件
    /// </summary>
    private Control CreateHeading(HeadingBlock headingBlock)
    {
        // 同样用 SelectableTextBlock 来显示标题，设置不同的字体大小/样式以示区别
        var container = new List<object>();
        if (headingBlock.Inline != null)
        {
            var controls = ConvertInlineContainer(headingBlock.Inline);
            foreach (var inl in controls)
            {
                if (inl is Inline inline)
                {
                    var fontSize = headingBlock.Level switch
                    {
                        1 => 24,
                        2 => 20,
                        3 => 18,
                        4 => 16,
                        5 => 14,
                        6 => 12,
                        _ => 12
                    };

                    // var text = new SelectableTextBlock();
                    // if (headingBlock.Level < 4)
                    // {
                    //     text.Classes.Add($"h{headingBlock.Level}");
                    // }
                    // else
                    // {
                    //     text.Classes.Add($"h4");
                    // }

                    if (container.LastOrDefault() is SelectableTextBlock span)
                    {
                        span.Inlines?.Add(inline);
                    }
                    else
                    {
                        span = new SelectableTextBlock
                        {
                            FontSize = fontSize, FontWeight = FontWeight.Bold, Inlines = new InlineCollection()
                        };
                        span.Inlines?.Add(inline);
                        container.Add(span);
                    }
                }
                else if (inl is Control control)
                {
                    container.Add(control);
                }
            }
        }

        var panel = new WrapPanel() { };

        foreach (var item in container)
        {
            if (item is SelectableTextBlock span)
            {
                panel.Children.Add(span);
            }
            else if (item is Control control)
            {
                panel.Children.Add(control);
            }
        }

        return panel;
    }

    /// <summary>
    /// 创建代码块控件
    /// </summary>
    private Control CreateCodeBlock(FencedCodeBlock fencedCodeBlock)
    {
        var border = new Border
        {
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(5),
            Margin = new Thickness(0, 5, 0, 5)
        };

        var stackPanel = new StackPanel { Orientation = Orientation.Vertical };

        // 头部面板：语言标签 + Copy 按钮
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 0, 5) // 与代码区域做简单区分
        };


        // 如果第一个是代码块，第二行是文件路径则显示执行按钮
        if (fencedCodeBlock.Lines.Count > 1)
        {
            // 语言标签，给个稍微暗点的前景色以区分
            var languageText =
                new SelectableTextBlock { Text = fencedCodeBlock.Info, Margin = new Thickness(0, 2, 10, 0) };

            // Copy 按钮，与代码区域色彩区分（示例：背景灰，前景白）
            var copyButton = new Button
            {
                Content = CopyText,
                FontSize = 12,
                Height = 24,
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 0)
            };

            // 根据当前系统主题设置按钮颜色
            if (Application.Current.RequestedThemeVariant == ThemeVariant.Light)
            {
                copyButton.Background = SolidColorBrush.Parse("#0078d4");
                copyButton.Foreground = SolidColorBrush.Parse("#ffffff");
            }
            else if (Application.Current.RequestedThemeVariant == ThemeVariant.Dark)
            {
                copyButton.Background = SolidColorBrush.Parse("#313131");
                copyButton.Foreground = SolidColorBrush.Parse("#ffffff");
            }
            else
            {
                copyButton.Background = SolidColorBrush.Parse("#313131");
                copyButton.Foreground = SolidColorBrush.Parse("#ffffff");
            }

            copyButton.Click += (sender, e) =>
            {
                CopyClick?.Invoke(this, e);
                var clipboard = TopLevel.GetTopLevel(this).Clipboard;
                clipboard.SetTextAsync(fencedCodeBlock.Lines.ToString());
                _notificationManager.Show(new Notification("复制成功", "代码已复制到剪贴板", NotificationType.Success));
            };

            headerPanel.Children.Add(languageText);
            headerPanel.Children.Add(copyButton);
        }

        var textBox = new TextBox
        {
            IsReadOnly = true,
            IsEnabled = true,
            Classes = { "markdown" },
            AcceptsReturn = true,
            Margin = new Thickness(5),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            TextWrapping = TextWrapping.WrapWithOverflow,
            Text = fencedCodeBlock.Lines.ToString()
        };

        stackPanel.Children.Add(headerPanel);

        // 获取当前系统主题

        if (Application.Current.RequestedThemeVariant == ThemeVariant.Light)
        {
            stackPanel.Children.Add(CodeRender.CodeRender.Render(textBox.Text, fencedCodeBlock.Info ?? "text",
                ThemeName.LightPlus));
            border.Child = stackPanel;
        }
        else
        {
            stackPanel.Children.Add(CodeRender.CodeRender.Render(textBox.Text, fencedCodeBlock.Info ?? "text",
                ThemeName.DarkPlus));
            border.Child = stackPanel;
        }


        return border;
    }


    /// <summary>
    /// 创建列表控件（有序或无序）
    /// </summary>
    private Control CreateList(ListBlock listBlock)
    {
        // 用 StackPanel 简单放置每个 ListItem
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };

        int orderIndex = 1; // 如果是有序列表，显示序号

        foreach (var item in listBlock)
        {
            if (item is ListItemBlock listItemBlock)
            {
                // 每个条目也可能包含段落、子列表等
                // 这里再递归调用 ConvertBlock
                // 为简化演示，每个 ListItem 先加一个“前缀 SelectableTextBlock”
                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                // 显示前缀
                var prefix = listBlock.IsOrdered
                    ? $"{orderIndex++}." // 有序列表：1. 2. 3. ...
                    : "  • "; // 无序列表：• • • •

                itemPanel.Children.Add(new SelectableTextBlock { Text = prefix, FontWeight = FontWeight.Bold, });

                // 再渲染该 listItemBlock 中的所有子块
                var subPanel = new StackPanel { Orientation = Orientation.Vertical };

                foreach (var subBlock in listItemBlock)
                {
                    var subControl = ConvertBlock(subBlock);
                    if (subControl != null)
                    {
                        subPanel.Children.Add(subControl);
                    }
                }

                itemPanel.Children.Add(subPanel);
                panel.Children.Add(itemPanel);
            }
        }

        return panel;
    }

    /// <summary>
    /// 将 Markdig 的容器 inline（ContainerInline）转换为一组 Avalonia 的 Inline
    /// </summary>
    private List<object> ConvertInlineContainer(ContainerInline containerInline)
    {
        var results = new List<object>();

        var child = containerInline?.FirstChild;
        while (child != null)
        {
            var controls = ConvertInline(child);
            foreach (var control in controls)
            {
                if (control is Inline inline)
                {
                    results.Add(inline);
                }
                else if (control is Control item)
                {
                    results.Add(item);
                }
            }

            child = child.NextSibling;
        }

        return results;
    }

    /// <summary>
    /// 将 Markdig 的单个 Inline 转为 Avalonia 的 Inline
    /// </summary>
    private List<object> ConvertInline(Markdig.Syntax.Inlines.Inline mdInline)
    {
        switch (mdInline)
        {
            // 斜体/加粗等富文本
            case EmphasisInline emphasisInline:
                return (CreateEmphasisInline(emphasisInline));

            // 行内代码
            case CodeInline codeInline:
                return [CreateCodeInline(codeInline)];

            // 图片
            case LinkInline { IsImage: true } linkInline:
                var image = CreateImageInline(linkInline);
                if (image != null)
                {
                    return [image];
                }

                return [];
            //
            // // 超链接
            case LinkInline linkInline:
                return [CreateHyperlinkInline(linkInline)];

            // 换行
            case LineBreakInline _:
                return [new LineBreak()];


            // 文字
            case LiteralInline literalInline:
                return [new Run(literalInline.Content.ToString())];


            case HtmlInline htmlInline:
                return [CreateHtmlInline(htmlInline)];

            // 其它暂未处理
            default:
                // 直接转成字符串
                return [new Run(mdInline.ToString())];
        }
    }

    private Control? CreateImageInline(LinkInline linkInline)
    {
        // literalInline可能是图片
        if (linkInline.IsImage && !string.IsNullOrEmpty(linkInline.Url))
        {
            var image = new ImagesRender() { Value = linkInline.Url, };

            return image;
        }

        return null;
    }

    private Inline CreateHtmlInline(HtmlInline htmlInline)
    {
        return new Run();
    }

    private Inline CreateHyperlinkInline(LinkInline linkInline)
    {
        foreach (var inline in linkInline)
        {
            if (inline is LiteralInline literalInline)
            {
                var span = new Span();
                var label = new SelectableTextBlock()
                {
                    Foreground = SolidColorBrush.Parse("#0078d4"),
                    TextDecorations = TextDecorations.Underline,
                    Text = literalInline.Content.ToString(),
                    // 为了让鼠标变成手型
                    Cursor = new Cursor(StandardCursorType.Hand),
                    // 当鼠标悬停时，显示一个 Tooltip
                };
                label.Classes.Add("link");

                // 判断label点击事件，它没有 Click 事件，所以监听鼠标按下事件 和 鼠标抬起事件，判断是否在同一位置
                label.Tapped += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(linkInline.Url)) return;
                    UrlHelper.OpenUrl(linkInline.Url);
                };

                span.Inlines.Add(label);

                return span;
            }
        }

        return new LineBreak();
    }

    /// <summary>
    /// 加粗/斜体处理
    /// </summary>
    private List<object> CreateEmphasisInline(EmphasisInline emphasis)
    {
        // EmphasisInline 里面也可能包含子 Inline
        var results = new List<object>();
        var controls = ConvertInlineContainer(emphasis);
        foreach (var c in controls)
        {
            if (c is Inline inline)
            {
                if (results.LastOrDefault() is Span span)
                {
                    span.Inlines.Add(inline);
                }
                else
                {
                    span = new Span { Inlines = { inline } };
                    // delimiterCount==2 表示 **加粗**，==1 表示 *斜体*
                    if (emphasis.DelimiterCount == 2)
                    {
                        span.FontWeight = FontWeight.Bold;
                    }
                    else if (emphasis.DelimiterCount == 1)
                    {
                        span.FontStyle = FontStyle.Italic;
                    }

                    results.Add(span);
                }
            }
            else if (c is Control item)
            {
                results.Add(item);
            }
        }


        return results;
    }

    /// <summary>
    /// 内联代码
    /// </summary>
    private Inline CreateCodeInline(CodeInline codeInline)
    {
        if (Application.Current.RequestedThemeVariant == ThemeVariant.Light)
        {
            return new Run(codeInline.Content)
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = SolidColorBrush.Parse("#000000"),
                Background = SolidColorBrush.Parse("#f0f0f0"),
            };
        }
        else if (Application.Current.RequestedThemeVariant == ThemeVariant.Dark)
        {
            return new Run(codeInline.Content)
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = SolidColorBrush.Parse("#f0f0f0"),
                Background = SolidColorBrush.Parse("#313131"),
            };
        }
        else
        {
            return new Run(codeInline.Content)
            {
                FontFamily = new FontFamily("Consolas"),
                Foreground = SolidColorBrush.Parse("#f0f0f0"),
                Background = SolidColorBrush.Parse("#313131"),
            };
        }
    }
}