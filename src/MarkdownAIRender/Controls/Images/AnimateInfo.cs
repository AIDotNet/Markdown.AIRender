namespace MarkdownAIRender.Controls.Images;

public record AnimateInfo
{
    public string? Text { get; set; }
    public string? PathData { get; set; }
    public double MoveDuration { get; set; }
    public string? MoveRepeatCount { get; set; }

    // 颜色动画参数 (已有)
    public string? FromColor { get; set; }
    public string? ToColor { get; set; }
    public double ColorDuration { get; set; }
    public string? ColorRepeatCount { get; set; }

    // ---- 新增：原始 <path> 的 stroke / stroke-width / fill
    public string? PathStroke { get; set; }
    public double PathStrokeWidth { get; set; }
    public string? PathFill { get; set; }
}