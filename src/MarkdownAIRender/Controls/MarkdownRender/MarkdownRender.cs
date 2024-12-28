using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Styling;
using Avalonia.VisualTree;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using MarkdownAIRender.Controls.Images;
using MarkdownAIRender.Helper;

using TextMateSharp.Grammars;

using Inline = Avalonia.Controls.Documents.Inline;

namespace MarkdownAIRender.Controls.MarkdownRender
{
    public class MarkdownRender : ContentControl, INotifyPropertyChanged
    {
        #region Dependency Property

        public static readonly StyledProperty<string?> ValueProperty =
            AvaloniaProperty.Register<MarkdownRender, string?>(nameof(Value));

        #endregion

        #region Fields

        // 旧的文档结构（用于对比增量渲染）
        private MarkdownDocument? _oldDocument;

        // 用于存储 Block -> Control 的映射
        private readonly Dictionary<Block, Control> _oldBlockControlMap = new();

        private MarkdownDocument? _parsedDocument;
        private WindowNotificationManager? _notificationManager;
        private string _copyText = "Copy";

        #endregion

        #region Events

        /// <summary>
        /// 用于处理代码块中可能的额外工具渲染，外部可以订阅并自定义渲染逻辑。
        /// </summary>
        public event CodeToolRenderEventHandler? CodeToolRenderEvent;

        /// <summary>
        /// 复制按钮触发事件
        /// </summary>
        public event EventHandler? CopyClick;

        /// <summary>
        /// 标准的 PropertyChanged 事件
        /// </summary>
        public new event PropertyChangedEventHandler? PropertyChanged;

        #endregion

        #region Properties

        public string? Value
        {
            get => GetValue(ValueProperty);
            set
            {
                SetValue(ValueProperty, value);
                OnPropertyChanged();

                // 每次 Value 改变时，重新解析+渲染
                ParseMarkdown(GetValue(ValueProperty));

                // 使用增量方式渲染
                RenderParsedDocumentIncremental();
            }
        }

        public string CopyText
        {
            get => _copyText;
            set
            {
                if (_copyText == value) return;
                _copyText = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Constructor

        public MarkdownRender()
        {
            // 构造函数里如果有初始Value, 可以主动解析
            if (!string.IsNullOrEmpty(GetValue(ValueProperty)))
            {
                ParseMarkdown(GetValue(ValueProperty));
            }

            // 监测 ValueProperty 的变化
            ValueProperty.Changed.AddClassHandler<MarkdownRender>((sender, e) =>
            {
                if (e.NewValue is string newValue)
                {
                    sender.Value = newValue;
                }
            });
        }

        #endregion

        #region Overrides

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (_parsedDocument == null && !string.IsNullOrEmpty(GetValue(ValueProperty)))
            {
                ParseMarkdown(GetValue(ValueProperty));
            }

            // 初始渲染（增量）
            RenderParsedDocumentIncremental();

            // 初始化通知管理器
            _notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(this))
            {
                Position = NotificationPosition.TopRight,
                MaxItems = 3,
                Margin = new Thickness(0, 0, 15, 40)
            };

            // 订阅主题变化事件
            Application.Current.ActualThemeVariantChanged += ThemeChanged;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            // 模板相关操作
        }

        #endregion

        #region Theme Handling

        private void ThemeChanged(object? sender, EventArgs e)
        {
            // 主题改变时也做一次增量渲染（或全量渲染）
            if (_parsedDocument != null)
            {
                RenderParsedDocumentIncremental();
            }
        }

        #endregion

        #region Parsing & Incremental Rendering

        private void ParseMarkdown(string? markdown)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                _parsedDocument = null;
                return;
            }

            _parsedDocument = Markdown.Parse(markdown);
        }

        /// <summary>
        /// 增量渲染：对比新旧文档，只在需要时新建或删除控件
        /// </summary>
        private void RenderParsedDocumentIncremental()
        {
            if (_parsedDocument == null)
            {
                Content = null;
                _oldDocument = null;
                _oldBlockControlMap.Clear();
                return;
            }

            // 若当前 Content 不是StackPanel，就换成一个新的
            if (Content is not StackPanel container)
            {
                container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
                Content = container;
                _oldDocument = null;
                _oldBlockControlMap.Clear();
            }

            // 对比新旧 Document
            DiffAndUpdateBlocks(container, _oldDocument, _parsedDocument);

            // 更新旧文档引用
            _oldDocument = _parsedDocument;
        }

        /// <summary>
        /// 对比新旧文档的 Blocks，并对 UI 做增量修改
        /// </summary>
        private void DiffAndUpdateBlocks(StackPanel container, MarkdownDocument? oldDoc, MarkdownDocument newDoc)
        {
            // 如果旧文档为空 => 全量添加
            if (oldDoc == null || oldDoc.Count == 0)
            {
                container.Children.Clear();
                _oldBlockControlMap.Clear();

                foreach (var newBlock in newDoc)
                {
                    var newControl = ConvertBlock(newBlock);
                    if (newControl != null)
                    {
                        container.Children.Add(newControl);
                        _oldBlockControlMap[newBlock] = newControl;
                    }
                }
                return;
            }

            var oldBlocks = oldDoc.ToList();
            var newBlocks = newDoc.ToList();

            var oldMapBackup = new Dictionary<Block, Control>(_oldBlockControlMap);

            var finalChildren = new List<Control>();
            var newBlockControlMap = new Dictionary<Block, Control>();

            int maxCount = Math.Max(oldBlocks.Count, newBlocks.Count);

            for (int i = 0; i < maxCount; i++)
            {
                var oldBlock = i < oldBlocks.Count ? oldBlocks[i] : null;
                var newBlock = i < newBlocks.Count ? newBlocks[i] : null;

                // 如果旧Block不存在 => 新增
                if (oldBlock == null && newBlock != null)
                {
                    var newCtrl = ConvertBlock(newBlock);
                    if (newCtrl != null)
                    {
                        finalChildren.Add(newCtrl);
                        newBlockControlMap[newBlock] = newCtrl;
                    }
                    continue;
                }

                // 如果新Block不存在 => 删除
                if (newBlock == null && oldBlock != null)
                {
                    if (oldMapBackup.TryGetValue(oldBlock, out var oldCtrl))
                    {
                        // 可以在这里做 oldCtrl 的清理，Dispose等
                    }
                    continue;
                }

                // 两边都存在 => 判断是否可复用
                if (oldBlock != null && newBlock != null)
                {
                    bool canReuse =
                        oldBlock.GetType() == newBlock.GetType()
                        && GetBlockContent(oldBlock) == GetBlockContent(newBlock);

                    if (canReuse && oldMapBackup.TryGetValue(oldBlock, out var oldCtrl))
                    {
                        // 可以复用
                        finalChildren.Add(oldCtrl);
                        newBlockControlMap[newBlock] = oldCtrl;
                    }
                    else
                    {
                        // 无法复用 => 移除旧控件，新建新控件
                        if (oldMapBackup.TryGetValue(oldBlock, out var oldCtrlToRemove))
                        {
                            // 做一些清理
                        }
                        var newCtrl = ConvertBlock(newBlock);
                        if (newCtrl != null)
                        {
                            finalChildren.Add(newCtrl);
                            newBlockControlMap[newBlock] = newCtrl;
                        }
                    }
                }
            }

            container.Children.Clear();
            foreach (var c in finalChildren)
            {
                container.Children.Add(c);
            }

            // 替换旧的映射表
            _oldBlockControlMap.Clear();
            foreach (var kv in newBlockControlMap)
            {
                _oldBlockControlMap[kv.Key] = kv.Value;
            }
        }

        #endregion

        #region Block Content Extraction (for comparison)

        /// <summary>
        /// 获取一个 Block 的“完整文本内容”，用来做比较。
        /// 如果内部还有子Block（如List、Quote），会递归收集。
        /// </summary>
        private string GetBlockContent(Block block)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    return GetInlineContent(paragraph.Inline);

                case HeadingBlock heading:
                    return "HEADING:" + heading.Level + ":" + GetInlineContent(heading.Inline);

                case FencedCodeBlock codeBlock:
                    return "CODE:" + codeBlock.Info + Environment.NewLine + codeBlock.Lines.ToString();

                case ListBlock listBlock:
                {
                    // 遍历子ItemBlock
                    var textList = new List<string>();
                    foreach (var subItem in listBlock)
                    {
                        textList.Add(GetBlockContent(subItem));
                    }
                    return (listBlock.IsOrdered ? "ORDERLIST:" : "BULLETLIST:") + string.Join("\n", textList);
                }

                case QuoteBlock quoteBlock:
                {
                    // 递归收集子Block
                    var quoteTexts = new List<string>();
                    foreach (var subBlock in quoteBlock)
                    {
                        quoteTexts.Add(GetBlockContent(subBlock));
                    }
                    return "QUOTE:" + string.Join("\n", quoteTexts);
                }

                case ThematicBreakBlock _:
                    return "THEMATICBREAK";

                default:
                    // 其它类型可以简单返回 block.ToString()
                    // 或做更深入的处理
                    return "OTHER:" + (block?.ToString() ?? "");
            }
        }

        /// <summary>
        /// 递归收集 InlineContainer 的文本内容
        /// </summary>
        private string GetInlineContent(ContainerInline? container)
        {
            if (container == null)
                return "";

            var textList = new List<string>();
            var current = container.FirstChild;

            while (current != null)
            {
                textList.Add(GetInlineText(current));
                current = current.NextSibling;
            }

            return string.Join("", textList);
        }

        /// <summary>
        /// 获取单个 Inline 的文本
        /// </summary>
        private string GetInlineText(Markdig.Syntax.Inlines.Inline mdInline)
        {
            switch (mdInline)
            {
                case EmphasisInline emphasisInline:
                {
                    // 根据是否**或*拼接特殊标记 + 递归内部
                    var delim = emphasisInline.DelimiterCount == 2 ? "**" : "*";
                    var subText = GetInlineContent(emphasisInline);
                    return delim + subText + delim;
                }
                case CodeInline codeInline:
                    return "`" + codeInline.Content + "`";

                case LinkInline linkInline when linkInline.IsImage:
                    return $"![{GetInlineContent(linkInline)}]({linkInline.Url})";

                case LinkInline linkInline:
                    return $"[{GetInlineContent(linkInline)}]({linkInline.Url})";

                case LineBreakInline _:
                    return "\n";

                case LiteralInline literalInline:
                    return literalInline.Content.ToString();

                case HtmlInline htmlInline:
                    return "<HTML>" + (htmlInline.Tag ?? "") + "</HTML>";

                default:
                    return mdInline.ToString() ?? "";
            }
        }

        #endregion

        #region Core Convert Methods

        private Control? ConvertBlock(Block block)
        {
            switch (block)
            {
                case ParagraphBlock paragraph:
                    return CreateParagraph(paragraph);

                case HeadingBlock heading:
                    return CreateHeading(heading);

                case FencedCodeBlock codeBlock:
                    return CreateCodeBlock(codeBlock);

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

                default:
                    // 其它类型（简单处理）
                    return new TextBox
                    {
                        IsReadOnly = true,
                        IsEnabled = true,
                        AcceptsReturn = true,
                        Classes = { "markdown" },
                        Margin = new Thickness(0),
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        TextWrapping = TextWrapping.Wrap,
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

        private Control CreateParagraph(ParagraphBlock paragraph)
        {
            var container = new WrapPanel { Orientation = Orientation.Horizontal };

            if (paragraph.Inline != null)
            {
                var controls = ConvertInlineContainer(paragraph.Inline);
                foreach (var control in controls)
                {
                    if (control is Inline inline)
                    {
                        if (container.Children.LastOrDefault() is SelectableTextBlock lastSpan)
                        {
                            lastSpan.Inlines?.Add(inline);
                        }
                        else
                        {
                            var span = new SelectableTextBlock
                            {
                                Inlines = new InlineCollection(),
                                TextWrapping = TextWrapping.Wrap,
                            };
                            span.Inlines?.Add(inline);
                            container.Children.Add(span);
                        }
                    }
                    else if (control is Control childControl)
                    {
                        container.Children.Add(childControl);
                    }
                }
            }

            return container;
        }

        private Control CreateHeading(HeadingBlock headingBlock)
        {
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

                        if (container.LastOrDefault() is SelectableTextBlock span)
                        {
                            span.Inlines?.Add(inline);
                        }
                        else
                        {
                            span = new SelectableTextBlock
                            {
                                FontSize = fontSize,
                                FontWeight = FontWeight.Bold,
                                TextWrapping = TextWrapping.Wrap,
                                Inlines = new InlineCollection()
                            };
                            span.Inlines?.Add(inline);
                            container.Add(span);
                        }
                    }
                    else if (inl is Control ctrl)
                    {
                        container.Add(ctrl);
                    }
                }
            }

            var panel = new WrapPanel();
            foreach (var item in container)
            {
                switch (item)
                {
                    case SelectableTextBlock span:
                        panel.Children.Add(span);
                        break;
                    case Control control:
                        panel.Children.Add(control);
                        break;
                }
            }

            return panel;
        }

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
                Margin = new Thickness(0, 0, 0, 5)
            };

            // 如果 CodeToolRenderEvent == null，走默认处理，否则交给外部
            if (fencedCodeBlock.Lines.Count > 0 && CodeToolRenderEvent == null)
            {
                var languageText = new SelectableTextBlock
                {
                    Text = fencedCodeBlock.Info,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 10, 0)
                };

                var copyButton = new Button
                {
                    Content = CopyText,
                    FontSize = 12,
                    Height = 24,
                    Padding = new Thickness(3),
                    Margin = new Thickness(0)
                };

                // 根据主题设置按钮颜色
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

                    _notificationManager?.Show(new Notification(
                        "复制成功",
                        "代码已复制到剪贴板",
                        NotificationType.Success));
                };

                headerPanel.Children.Add(languageText);
                headerPanel.Children.Add(copyButton);
                stackPanel.Children.Add(headerPanel);

                // 添加代码高亮控件 (假设你有 CodeRender.CodeRender)
                if (Application.Current.RequestedThemeVariant == ThemeVariant.Light)
                {
                    stackPanel.Children.Add(CodeRender.CodeRender.Render(
                        fencedCodeBlock.Lines.ToString(),
                        fencedCodeBlock.Info ?? "text",
                        ThemeName.LightPlus));
                }
                else
                {
                    stackPanel.Children.Add(CodeRender.CodeRender.Render(
                        fencedCodeBlock.Lines.ToString(),
                        fencedCodeBlock.Info ?? "text",
                        ThemeName.DarkPlus));
                }
            }
            else
            {
                // 如果有外部订阅 CodeToolRenderEvent，则交给外部自定义
                stackPanel.Children.Add(headerPanel);
                CodeToolRenderEvent?.Invoke(headerPanel, stackPanel, fencedCodeBlock);
            }

            border.Child = stackPanel;
            return border;
        }

        private Control CreateList(ListBlock listBlock)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
            int orderIndex = 1; // 有序列表的起始索引

            foreach (var item in listBlock)
            {
                if (item is ListItemBlock listItemBlock)
                {
                    var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                    var prefix = listBlock.IsOrdered ? $"{orderIndex++}." : "• ";
                    itemPanel.Children.Add(new SelectableTextBlock
                    {
                        Text = prefix,
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.Bold
                    });

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

        #endregion

        #region Inline Handling

        private List<object> ConvertInlineContainer(ContainerInline containerInline)
        {
            var results = new List<object>();
            var child = containerInline?.FirstChild;

            while (child != null)
            {
                var controls = ConvertInline(child);
                results.AddRange(controls);
                child = child.NextSibling;
            }

            return results;
        }

        private List<object> ConvertInline(Markdig.Syntax.Inlines.Inline mdInline)
        {
            switch (mdInline)
            {
                case EmphasisInline emphasisInline:
                    return CreateEmphasisInline(emphasisInline);

                case CodeInline codeInline:
                    return new List<object> { CreateCodeInline(codeInline) };

                case LinkInline { IsImage: true } linkImg:
                {
                    var img = CreateImageInline(linkImg);
                    return img != null ? new List<object> { img } : new List<object>();
                }
                case LinkInline linkInline:
                    return new List<object> { CreateHyperlinkInline(linkInline) };

                case LineBreakInline _:
                    return new List<object> { new LineBreak() };

                case LiteralInline literalInline:
                    return new List<object> { new Run(literalInline.Content.ToString()) };

                case HtmlInline htmlInline:
                    return new List<object> { CreateHtmlInline(htmlInline) };

                default:
                    return new List<object> { new Run(mdInline.ToString()) };
            }
        }

        private Control? CreateImageInline(LinkInline linkInline)
        {
            if (!string.IsNullOrEmpty(linkInline.Url))
            {
                return new ImagesRender { Value = linkInline.Url };
            }
            return null;
        }

        private Inline CreateHtmlInline(HtmlInline htmlInline)
        {
            // 简单处理：实际可进一步解析 htmlInline.Tag
            return new Run();
        }

        private Inline CreateHyperlinkInline(LinkInline linkInline)
        {
            foreach (var inline in linkInline)
            {
                if (inline is LiteralInline literalInline)
                {
                    var span = new Span();
                    var label = new SelectableTextBlock
                    {
                        Foreground = SolidColorBrush.Parse("#0078d4"),
                        TextDecorations = TextDecorations.Underline,
                        TextWrapping = TextWrapping.Wrap,
                        Text = literalInline.Content.ToString(),
                        Cursor = new Cursor(StandardCursorType.Hand)
                    };
                    label.Classes.Add("link");

                    label.Tapped += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(linkInline.Url))
                            UrlHelper.OpenUrl(linkInline.Url);
                    };

                    span.Inlines.Add(label);
                    return span;
                }
            }
            return new LineBreak();
        }

        private List<object> CreateEmphasisInline(EmphasisInline emphasis)
        {
            var results = new List<object>();
            var controls = ConvertInlineContainer(emphasis);

            foreach (var c in controls)
            {
                if (c is Inline inline)
                {
                    if (results.LastOrDefault() is Span lastSpan)
                    {
                        lastSpan.Inlines.Add(inline);
                    }
                    else
                    {
                        var spanNew = new Span { Inlines = { inline } };
                        if (emphasis.DelimiterCount == 2)
                            spanNew.FontWeight = FontWeight.Bold;
                        else if (emphasis.DelimiterCount == 1)
                            spanNew.FontStyle = FontStyle.Italic;

                        results.Add(spanNew);
                    }
                }
                else if (c is Control ctrl)
                {
                    results.Add(ctrl);
                }
            }

            return results;
        }

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

        #endregion

        #region INotifyPropertyChanged Support

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}