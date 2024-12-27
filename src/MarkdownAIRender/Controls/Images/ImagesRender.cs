using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace MarkdownAIRender.Controls.Images;

public class ImagesRender : UserControl
{
    private static readonly HttpClient HttpClient = new();

    static ImagesRender()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
    }

    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<ImagesRender, string?>(
        nameof(Value));


    public string? Value
    {
        get => GetValue(ValueProperty);
        set
        {
            SetValue(ValueProperty, value);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (Value != null)
        {
            _ = LoadImageAsync(Value);
        }
    }

    public async Task LoadImageAsync(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return;
            }


            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // 获取图片宽度高度
                var decoder = new Image
                {
                    Stretch = Stretch.Uniform, Source = new Bitmap(await response.Content.ReadAsStreamAsync()),
                    // 自适应
                };
                decoder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                decoder.Arrange(new Rect(decoder.DesiredSize));
                decoder.Margin = new Thickness(10);

                Content = decoder;
            });
        }
        catch (Exception)
        {
            // ignored
        }
    }
}