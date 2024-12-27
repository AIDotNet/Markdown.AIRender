using System;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;

using TextMateSharp.Grammars;
using TextMateSharp.Registry;

// 注意这里，避免跟 Avalonia.Media.FontStyle 冲突
// 我们将 TextMateSharp 的 FontStyle 取别名
using TmFontStyle = TextMateSharp.Themes.FontStyle;

namespace MarkdownAIRender.CodeRender
{
    public static class CodeRender
    {
        // 用于跨行保存解析状态
        private static IStateStack? s_ruleStack;

        public static Control Render(string code, string language, ThemeName themeName)
        {
            var options = new RegistryOptions(themeName);
            var registry = new Registry(options);
            var theme = registry.GetTheme();

            IGrammar grammar = registry.LoadGrammar(
                // 你也可以改为：options.GetScopeByExtension(language)
                options.GetScopeByLanguageId(language)
            );

            if (grammar == null)
            {
                // 如果没有对应的 grammar，就简单返回纯文本
                return new SelectableTextBlock { Text = code,TextWrapping = TextWrapping.Wrap};
            }

            // 使用 SelectableTextBlock，使得代码可被复制
            var textBlock = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };

            s_ruleStack = null;
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                var lineResult = grammar.TokenizeLine(line, s_ruleStack, TimeSpan.MaxValue);
                s_ruleStack = lineResult.RuleStack;

                foreach (var token in lineResult.Tokens)
                {
                    int startIndex = Math.Min(token.StartIndex, line.Length);
                    int endIndex = Math.Min(token.EndIndex, line.Length);
                    if (endIndex <= startIndex) continue;

                    string tokenText = line.Substring(startIndex, endIndex - startIndex);

                    // 分析该 token 的所有 themeRule，并叠加样式
                    int foregroundId = -1;
                    int backgroundId = -1;
                    TmFontStyle fontStyle = TmFontStyle.NotSet;

                    var matchedRules = theme.Match(token.Scopes);
                    foreach (var rule in matchedRules)
                    {
                        // 前景色：只要第一个规则设置了就用它
                        if (foregroundId == -1 && rule.foreground > 0)
                            foregroundId = rule.foreground;

                        // 背景色：只要第一个规则设置了就用它
                        if (backgroundId == -1 && rule.background > 0)
                            backgroundId = rule.background;

                        // 字体样式：这里用 位或(|=) 累加
                        if (rule.fontStyle > 0)
                            fontStyle |= (TmFontStyle)rule.fontStyle;
                    }

                    IImmutableSolidColorBrush fgBrush;
                    if (Application.Current.RequestedThemeVariant == ThemeVariant.Light)
                    {
                        fgBrush = foregroundId == -1
                            ? Brushes.Black
                            : new ImmutableSolidColorBrush(HexToColor(theme.GetColor(foregroundId)));
                    }
                    else if (Application.Current.RequestedThemeVariant == ThemeVariant.Dark)
                    {
                        fgBrush = foregroundId == -1
                            ? Brushes.White
                            : new ImmutableSolidColorBrush(HexToColor(theme.GetColor(foregroundId)));
                    }
                    else if (Application.Current.RequestedThemeVariant == ThemeVariant.Default)
                    {
                        // 默认主题，根据当前主题自动切换，获取当前系统主题

                        fgBrush = foregroundId == -1
                            ? Brushes.White
                            : new ImmutableSolidColorBrush(HexToColor(theme.GetColor(foregroundId)));
                    }
                    else
                    {
                        fgBrush = foregroundId == -1
                            ? Brushes.White
                            : new ImmutableSolidColorBrush(HexToColor(theme.GetColor(foregroundId)));
                    }

                    var bgBrush = backgroundId == -1
                        ? null
                        : new ImmutableSolidColorBrush(HexToColor(theme.GetColor(backgroundId)));

                    // 创建一个 Run
                    var run = new Run { Text = tokenText, Foreground = fgBrush, Background = bgBrush };

                    // 设置下划线
                    if (fontStyle == TmFontStyle.Underline)
                    {
                        run.TextDecorations =
                        [
                            new TextDecoration
                            {
                                Location = TextDecorationLocation.Underline,
                                StrokeThicknessUnit = TextDecorationUnit.Pixel
                            }
                        ];
                    }

                    // 设置加粗
                    if (fontStyle == TmFontStyle.Bold)
                    {
                        run.FontWeight = FontWeight.Bold;
                    }

                    // 设置斜体
                    if (fontStyle == TmFontStyle.Italic)
                    {
                        run.FontStyle = Avalonia.Media.FontStyle.Italic;
                    }

                    textBlock.Inlines.Add(run);
                }

                // 每行结束，手动换行
                textBlock.Inlines.Add(new LineBreak());
            }

            return new ScrollViewer()
            {
                Content = textBlock
            };
        }

        private static Color HexToColor(string hexString)
        {
            if (hexString.StartsWith('#'))
                hexString = hexString.Substring(1);

            byte r = byte.Parse(hexString.Substring(0, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hexString.Substring(2, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hexString.Substring(4, 2), NumberStyles.HexNumber);

            return Color.FromRgb(r, g, b);
        }
    }
}