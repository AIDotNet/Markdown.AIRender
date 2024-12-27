using System.ComponentModel;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Render(Value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
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

    /// <summary>
    /// 创建段落控件
    /// </summary>
    private Control CreateParagraph(ParagraphBlock paragraph)
    {
        // 用 TextBlock 来呈现段落中的文本/行内元素
        var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap, };

        // 段落里包含一系列的 Inline (Markdig 类型)，需要转成 Avalonia 的 Inline
        if (paragraph.Inline != null)
        {
            var inlines = ConvertInlineContainer(paragraph.Inline);
            foreach (var inl in inlines)
            {
                textBlock.Inlines?.Add(inl);
            }
        }

        return textBlock;
    }

    /// <summary>
    /// 创建标题控件
    /// </summary>
    private Control CreateHeading(HeadingBlock headingBlock)
    {
        // 同样用 TextBlock 来显示标题，设置不同的字体大小/样式以示区别
        var textBlock = new TextBlock { FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };

        switch (headingBlock.Level)
        {
            case 1:
                textBlock.FontSize = 24;
                break;
            case 2:
                textBlock.FontSize = 20;
                break;
            case 3:
                textBlock.FontSize = 18;
                break;
            default:
                textBlock.FontSize = 16;
                break;
        }

        if (headingBlock.Inline != null)
        {
            var inlines = ConvertInlineContainer(headingBlock.Inline);
            foreach (var inl in inlines)
            {
                textBlock.Inlines.Add(inl);
            }
        }

        return textBlock;
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
            var languageText = new TextBlock { Text = fencedCodeBlock.Info, Margin = new Thickness(0, 2, 10, 0) };

            // Copy 按钮，与代码区域色彩区分（示例：背景灰，前景白）
            var copyButton = new Button
            {
                Content = CopyText,
                FontSize = 12,
                Height = 24,
                Padding = new Thickness(3),
                Margin = new Thickness(0, 0, 0, 0)
            };

            copyButton.Click += (sender, e) =>
            {
                CopyClick?.Invoke(this, e);
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
        stackPanel.Children.Add(textBox);
        border.Child = stackPanel;

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
                // 为简化演示，每个 ListItem 先加一个“前缀 TextBlock”
                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                // 显示前缀
                var prefix = listBlock.IsOrdered
                    ? $"{orderIndex++}." // 有序列表：1. 2. 3. ...
                    : "  • "; // 无序列表：• • • •

                itemPanel.Children.Add(new TextBlock { Text = prefix, FontWeight = FontWeight.Bold, });

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
    private IEnumerable<Inline> ConvertInlineContainer(ContainerInline containerInline)
    {
        var results = new List<Inline>();

        var child = containerInline?.FirstChild;
        while (child != null)
        {
            var avaloniaInline = ConvertInline(child);
            if (avaloniaInline != null)
            {
                results.Add(avaloniaInline);
            }

            child = child.NextSibling;
        }

        return results;
    }

    /// <summary>
    /// 将 Markdig 的单个 Inline 转为 Avalonia 的 Inline
    /// </summary>
    private Inline? ConvertInline(Markdig.Syntax.Inlines.Inline mdInline)
    {
        switch (mdInline)
        {
            // 斜体/加粗等富文本
            case EmphasisInline emphasisInline:
                return CreateEmphasisInline(emphasisInline);

            // 行内代码
            case CodeInline codeInline:
                return CreateCodeInline(codeInline);
            //
            // // 超链接
            case LinkInline linkInline:
                return CreateHyperlinkInline(linkInline);

            // 换行
            case LineBreakInline _:
                return new LineBreak();

            // 文字
            case LiteralInline literalInline:
                return new Run(literalInline.Content.ToString());

            // 其它暂未处理
            default:
                // 直接转成字符串
                return new Run(mdInline.ToString());
        }
    }

    private Inline CreateHyperlinkInline(LinkInline linkInline)
    {
        foreach (var inline in linkInline)
        {
            if(inline is LiteralInline literalInline)
            {
                var span = new Span();
                span.Inlines.Add(new Run(literalInline.Content.ToString())
                {
                    Foreground = SolidColorBrush.Parse("#0078d4"),
                    TextDecorations = TextDecorations.Underline
                });
                
                return span;
            }
        }
        
        return new LineBreak();
    }

    /// <summary>
    /// 加粗/斜体处理
    /// </summary>
    private Inline CreateEmphasisInline(EmphasisInline emphasis)
    {
        // EmphasisInline 里面也可能包含子 Inline
        var span = new Span();
        var children = ConvertInlineContainer(emphasis);
        foreach (var c in children)
        {
            span.Inlines.Add(c);
        }

        // delimiterCount==2 表示 **加粗**，==1 表示 *斜体*
        if (emphasis.DelimiterCount == 2)
        {
            span.FontWeight = FontWeight.Bold;
        }
        else if (emphasis.DelimiterCount == 1)
        {
            span.FontStyle = FontStyle.Italic;
        }

        return span;
    }

    /// <summary>
    /// 内联代码
    /// </summary>
    private Inline CreateCodeInline(CodeInline codeInline)
    {
        return new Run(codeInline.Content)
        {
            FontFamily = new FontFamily("Consolas"),
            Foreground = SolidColorBrush.Parse("#f0f0f0"),
            Background = SolidColorBrush.Parse("#313131"),
        };
    }
}