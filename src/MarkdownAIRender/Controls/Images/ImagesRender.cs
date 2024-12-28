using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MarkdownAIRender.Controls.Images
{
    public class ImagesRender : UserControl
    {
        private static readonly HttpClient HttpClient = new();

        static ImagesRender()
        {
            HttpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/58.0.3029.110 Safari/537.3");
        }

        public static readonly StyledProperty<string?> ValueProperty =
            AvaloniaProperty.Register<ImagesRender, string?>(nameof(Value));

        public string? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (!string.IsNullOrEmpty(Value))
            {
                _ = LoadImageAsync(Value!);
            }
        }

        /// <summary>
        /// Load image either from base64 string, local file path, or remote URL.
        /// </summary>
        /// <param name="input">Base64, file path, or URL to image.</param>
        public async Task LoadImageAsync(string input)
        {
            try
            {
                // 1. Check if the string is a data URI (base64)
                if (IsDataUri(input))
                {
                    await LoadImageFromBase64(input);
                }
                // 2. Check if it's a valid local file or a direct file:// reference
                else if (IsLocalFile(input))
                {
                    await LoadImageFromLocalFile(input);
                }
                // 3. Otherwise, assume it's a remote URL
                else
                {
                    await LoadImageFromRemote(input);
                }
            }
            catch
            {
                // ignored
            }
        }

        private bool IsDataUri(string input)
        {
            // Basic detection for data URI: "data:image/<type>;base64,<data>"
            return input.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
                && input.Contains("base64,");
        }

        private bool IsLocalFile(string input)
        {
            // You can customize checks: 
            //   - If it's an existing file path in your OS 
            //   - Or a file:// URI 
            // Simplest approach: check if it exists on disk or starts with "file://"
            if (Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                // If "file://..." or "C:\..." or relative path
                if (uri.IsFile)
                    return true;
            }

            // Or just check if physically exists
            return File.Exists(input);
        }

        private async Task LoadImageFromBase64(string dataUri)
        {
            // Strip out "data:image/png;base64," or similar prefix
            var base64Data = dataUri.Substring(dataUri.IndexOf(',') + 1);

            // Convert base64 to a byte array
            var bytes = Convert.FromBase64String(base64Data);

            // Load into Avalonia Bitmap
            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var imageCtrl = CreateImageControl(bitmap);
                Content = imageCtrl;
            });
        }

        private async Task LoadImageFromLocalFile(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var bitmap = new Bitmap(fileStream);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var imageCtrl = CreateImageControl(bitmap);
                Content = imageCtrl;
            });
        }

        private async Task LoadImageFromRemote(string url)
        {
            var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return;

            var stream = await response.Content.ReadAsStreamAsync();
            var bitmap = new Bitmap(stream);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var imageCtrl = CreateImageControl(bitmap);
                Content = imageCtrl;
            });
        }

        private Image CreateImageControl(Bitmap bitmap)
        {
            var imageControl = new Image
            {
                Stretch = Stretch.Uniform,
                Source = bitmap,
                Margin = new Thickness(10)
            };

            // If you want to measure/arrange:
            imageControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            imageControl.Arrange(new Rect(imageControl.DesiredSize));

            return imageControl;
        }
    }
}