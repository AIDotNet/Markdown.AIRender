using Avalonia.Controls;

using Markdig.Syntax;

namespace MarkdownAIRender;

public delegate void CodeToolRenderEventHandler(StackPanel headerPanel, StackPanel stackPanel, FencedCodeBlock fencedCodeBlock);
