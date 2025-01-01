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

using AvaloniaXmlTranslator;

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

        // 记录上一次完整的 Markdown 文本
        private string? _oldMarkdown = string.Empty;

        // 当前解析后的 MarkdownDocument
        private MarkdownDocument? _parsedDocument;

        private WindowNotificationManager? _notificationManager;

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

                // 当 Value 改变时，先尝试安全边界的增量渲染
                RenderDocumentSafeAppend();
            }
        }

        #endregion

        #region Constructor

        public MarkdownRender()
        {
            // 如果有初始 Value
            if (!string.IsNullOrEmpty(GetValue(ValueProperty)))
            {
                // 先记录
                _oldMarkdown = GetValue(ValueProperty);
                // 初次解析
                _parsedDocument = Markdown.Parse(_oldMarkdown);
            }

            // 监测 ValueProperty 的变化
            ValueProperty.Changed.AddClassHandler<MarkdownRender>((sender, e) =>
            {
                if (e.NewValue is string newValue && e.NewValue != Value)
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
                _oldMarkdown = GetValue(ValueProperty);
                _parsedDocument = Markdown.Parse(_oldMarkdown);
            }

            // 初次渲染：可直接做全量渲染或安全增量
            RenderDocumentSafeAppend();

            // 初始化通知管理器
            _notificationManager = new WindowNotificationManager(TopLevel.GetTopLevel(this))
            {
                Position = NotificationPosition.TopRight, MaxItems = 3, Margin = new Thickness(0, 0, 15, 40)
            };

            // 订阅主题变化事件
            Application.Current.ActualThemeVariantChanged += ThemeChanged;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            // 模板相关操作（如果有需要的话）
        }

        #endregion

        #region Theme Handling

        private void ThemeChanged(object? sender, EventArgs e)
        {
            // 主题改变时也做一次刷新
            if (_parsedDocument != null)
            {
                // 这里简单处理：直接全量刷新
                RebuildAll(_parsedDocument);
            }
        }

        #endregion

        #region Parsing & Rendering (Safe Append)

        /// <summary>
        /// 主入口：尝试从“安全边界”处做增量更新，如果不适用则直接全量渲染。
        /// </summary>
        private void RenderDocumentSafeAppend()
        {
            string newMarkdown = GetValue(ValueProperty) ?? string.Empty;

            // 先做完整解析，避免上下文丢失
            var newDoc = Markdown.Parse(newMarkdown);

            // 如果 oldMarkdown 是 newMarkdown 的前缀，而且长度更短 => 说明是“尾部追加”的可能性
            if (!string.IsNullOrEmpty(_oldMarkdown)
                && newMarkdown.StartsWith(_oldMarkdown)
                && newMarkdown.Length > _oldMarkdown.Length)
            {
                // 试图找一个安全边界
                int boundaryIndex = FindSafeBoundary(_oldMarkdown);
                // 如果找不到边界，或在末尾 => 无法安全部分渲染，直接全量
                if (boundaryIndex < 0 || boundaryIndex >= _oldMarkdown.Length)
                {
                    RebuildAll(newDoc);
                }
                else
                {
                    // 从该边界开始替换 UI
                    RebuildFromBoundary(boundaryIndex, newDoc);
                }
            }
            else
            {
                // 否则就全量渲染
                RebuildAll(newDoc);
            }

            // 更新当前状态
            _oldMarkdown = newMarkdown;
            _parsedDocument = newDoc;
        }

        /// <summary>
        /// 找到一个“安全边界”，这里以“最后一次换行符”作为示例。
        /// 你也可以改成找“最后一段空行”或“Markdig AST 的最近安全块”。
        /// </summary>
        private int FindSafeBoundary(string oldMarkdown)
        {
            // 简单找最后一次换行符
            // 如果想要更安全，可以找“```”或空行(\n\n)等
            int idx = oldMarkdown.LastIndexOf('\n');
            return idx; // -1表示没找到换行
        }

        /// <summary>
        /// 将整个 newDoc 重新生成 UI（全量刷新）。
        /// </summary>
        private void RebuildAll(MarkdownDocument newDoc)
        {
            if (newDoc == null || newDoc.Count == 0)
            {
                Content = null;
                return;
            }

            var container = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };

            foreach (var block in newDoc)
            {
                var newControl = ConvertBlock(block);
                if (newControl != null)
                {
                    container.Children.Add(newControl);
                }
            }

            Content = container;
        }

        /// <summary>
        /// 从 boundaryIndex 对应的区域开始，清理旧UI，然后用 newDoc 中对应的块重新生成。
        /// </summary>
        private void RebuildFromBoundary(int boundaryIndex, MarkdownDocument newDoc)
        {
            // 如果当前 Content 不是 StackPanel，就干脆全量渲染
            if (Content is not StackPanel container)
            {
                RebuildAll(newDoc);
                return;
            }

            // 找到“旧文档”里 boundaryIndex 所在的块下标
            int blockIndex = FindBlockIndexByOffset(_parsedDocument, boundaryIndex);

            // 如果找不到有效的 blockIndex，就全量
            if (blockIndex < 0)
            {
                RebuildAll(newDoc);
                return;
            }

            // 移除 container 中从 blockIndex 之后的所有子控件
            for (int i = container.Children.Count - 1; i >= blockIndex; i--)
            {
                container.Children.RemoveAt(i);
            }

            // 然后把 newDoc 中 blockIndex 之后的那些块转换添加进来
            for (int i = blockIndex; i < newDoc.Count; i++)
            {
                var ctrl = ConvertBlock(newDoc[i]);
                if (ctrl != null)
                {
                    container.Children.Add(ctrl);
                }
            }
        }

        /// <summary>
        /// 根据给定的偏移量 boundaryIndex，找出旧文档 _parsedDocument 中对应的块索引。
        /// 这里需要依赖 Markdig 的 SourceSpan 或 Lines 信息来计算。
        /// </summary>
        private int FindBlockIndexByOffset(MarkdownDocument? oldDoc, int boundaryIndex)
        {
            if (oldDoc == null) return -1;

            // Markdig 的 Block 有一个 Span 属性 (SourceSpan) 记录文本范围
            // 这里就简单找第一个“Span.End >= boundaryIndex”的 block
            for (int i = 0; i < oldDoc.Count; i++)
            {
                var block = oldDoc[i];
                if (block.Span.End >= boundaryIndex)
                {
                    return i;
                }
            }

            // 如果 boundaryIndex 超过了所有 block 的范围，返回 -1
            return -1;
        }

        #endregion

        #region Block & Inline Convert (基本与原代码一致)

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
                    return new SelectableTextBlock
                    {
                        IsEnabled = true,
                        Classes = { "markdown" },
                        Margin = new Thickness(0),
                        Background = Brushes.Transparent,
                        TextWrapping = TextWrapping.Wrap,
                        Text = block.ToString()
                    };
            }
        }

        private Control CreateParagraph(ParagraphBlock paragraph)
        {
            var container = new SelectableTextBlock() { TextWrapping = TextWrapping.Wrap, };

            if (paragraph.Inline != null)
            {
                var controls = ConvertInlineContainer(paragraph.Inline);
                foreach (var control in controls)
                {
                    if (control is Inline inline)
                    {
                        container.Inlines.Add(inline);
                    }
                    else if (control is Control childControl)
                    {
                        container.Inlines.Add(childControl);
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
                        if (container.LastOrDefault() is SelectableTextBlock span)
                        {
                            span.Inlines?.Add(inline);
                        }
                        else
                        {
                            span = new SelectableTextBlock
                            {
                                //Classes = { headingBlock.Level <= 6 ? $"MdH{headingBlock.Level}" : "MdHn" },
                                TextWrapping = TextWrapping.Wrap, Inlines = new InlineCollection()
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

            var panel = new SelectableTextBlock()
            {
                TextWrapping = TextWrapping.Wrap, Inlines = new InlineCollection()
            };
            foreach (var item in container)
            {
                switch (item)
                {
                    case SelectableTextBlock span:
                        panel.Inlines.Add(span);
                        break;
                    case Control control:
                        panel.Inlines.Add(control);
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
                    Content = "Copy",
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
                        I18nManager.Instance.GetResource(Localization.MarkdownRender.CopyNotificationTitle),
                        I18nManager.Instance.GetResource(Localization.MarkdownRender.CopyNotificationMessage),
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
                    var itemPanel = new WrapPanel()
                    {
                        Orientation = Orientation.Horizontal
                    };

                    var prefix = listBlock.IsOrdered ? $"{orderIndex++}." : "• ";
                    itemPanel.Children.Add(new SelectableTextBlock
                    {
                        Text = prefix,
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.Bold,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    foreach (var subBlock in listItemBlock)
                    {
                        var subControl = ConvertBlock(subBlock);
                        if (subControl != null)
                        {
                            itemPanel.Children.Add(subControl);
                        }
                        // 如果是SelectableTextBlock，则将内容添加到 itemPanel而不是subPanel
                        // if (subControl is SelectableTextBlock selectableTextBlock)
                        // {
                        //     itemPanel.Inlines.Add(selectableTextBlock);
                        // }
                    }

                    // itemPanel.Inlines.Add(subPanel);
                    panel.Children.Add(itemPanel);
                }
            }

            return panel;
        }


        private Control CreateQuote(QuoteBlock quoteBlock)
        {
            var border = new Border();
            border.AddMdClass("MdQuoteBorder");

            var stackPanel = new StackPanel();
            stackPanel.AddMdClass("MdQuoteStackPanel");

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
                    // 其它情况：简单转成文字
                    return new List<object> { new Run(mdInline.ToString()) };
            }
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
                        //Classes = { "MdLink" },
                        Text = literalInline.Content.ToString(),
                        TextWrapping = TextWrapping.Wrap,
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

            // 如果没有 literalInline，就简单换行
            return new LineBreak();
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