using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Skia; // <-- 关键
using SkiaSharp; // <-- 关键
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

using Avalonia.Svg;

using ShimSkiaSharp;

using Svg.Model;

using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using SKPath = SkiaSharp.SKPath;
using SKPoint = SkiaSharp.SKPoint;

namespace MarkdownAIRender.Controls.Images
{
    public class ImagesRender : UserControl
    {
        private static readonly HttpClient HttpClient = new();
        private static readonly AvaloniaAssetLoader SvgAssetLoader = new();

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

        private static bool IsSvgFile(Stream fileStream)
        {
            try
            {
                int firstChr = fileStream.ReadByte();
                if (firstChr != ('<' & 0xFF))
                    return false;

                fileStream.Seek(0, SeekOrigin.Begin);
                using var xmlReader = XmlReader.Create(fileStream);
                return xmlReader.MoveToContent() == XmlNodeType.Element &&
                       "svg".Equals(xmlReader.Name, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
            finally
            {
                fileStream.Seek(0, SeekOrigin.Begin);
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
                if (IsDataUri(input))
                {
                    await LoadImageFromBase64(input);
                }
                else if (IsLocalFile(input))
                {
                    await LoadImageFromLocalFile(input);
                }
                else
                {
                    await LoadImageFromRemote(input);
                }
            }
            catch
            {
                // ignore
            }
        }

        private bool IsDataUri(string input)
        {
            return input.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
                   && input.Contains("base64,");
        }

        private bool IsLocalFile(string input)
        {
            if (Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                if (uri.IsFile)
                    return true;
            }

            return File.Exists(input);
        }

        private async Task LoadImageFromBase64(string dataUri)
        {
            var base64Data = dataUri.Substring(dataUri.IndexOf(',') + 1);
            var bytes = Convert.FromBase64String(base64Data);

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
            await using var fileStream = File.OpenRead(filePath);

            if (filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || IsSvgFile(fileStream))
            {
                fileStream.Seek(0, SeekOrigin.Begin);
                string svgXml;
                using (var reader = new StreamReader(fileStream))
                {
                    svgXml = await reader.ReadToEndAsync();
                }

                if (TryExtractAnimateMotionAndColor(svgXml, out var animInfo))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var animatedCtrl = new AnimatedSvgTextControl(animInfo);
                        Content = animatedCtrl;
                    });
                }
                else
                {
                    // 静态 SVG
                    using var memStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svgXml));
                    var document = SvgExtensions.Open(memStream);
                    var picture = document is not null
                        ? SvgExtensions.ToModel(document, SvgAssetLoader, out _, out _)
                        : null;
                    var svgsrc = new SvgSource() { Picture = picture };
                    var svg = (IImage)new VectorImage() { Source = svgsrc };

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Content = new Border
                        {
                            Child = new Image
                            {
                                Source = svg, Stretch = Stretch.Uniform, Margin = new Thickness(10)
                            }
                        };
                    });
                }

                return;
            }

            fileStream.Seek(0, SeekOrigin.Begin);
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

            var bytes = await response.Content.ReadAsByteArrayAsync();
            using var memStream = new MemoryStream(bytes);

            if (url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || IsSvgFile(memStream))
            {
                memStream.Seek(0, SeekOrigin.Begin);
                string svgXml;
                using (var reader = new StreamReader(memStream))
                {
                    svgXml = await reader.ReadToEndAsync();
                }

                if (TryExtractAnimateMotionAndColor(svgXml, out var animInfo))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        var animatedCtrl = new AnimatedSvgTextControl(animInfo);
                        Content = new Border { Child = animatedCtrl };
                    });
                }
                else
                {
                    // 静态 SVG
                    var document = SvgExtensions.Open(memStream);
                    var picture = document is not null
                        ? SvgExtensions.ToModel(document, SvgAssetLoader, out _, out _)
                        : null;
                    var svgsrc = new SvgSource() { Picture = picture };
                    var svg = (IImage)new VectorImage() { Source = svgsrc };

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Content = new Border
                        {
                            Child = new Image
                            {
                                Source = svg, Stretch = Stretch.Uniform, Margin = new Thickness(10)
                            }
                        };
                    });
                }

                return;
            }

            memStream.Seek(0, SeekOrigin.Begin);
            var bitmap = new Bitmap(memStream);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var imageCtrl = CreateImageControl(bitmap);
                Content = imageCtrl;
            });
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            // 如果你希望固定默认大小，比如 600×400
            // 若外部给的空间更大，则实际可扩展，但至少不会是 0×0
            var desiredWidth = 600;
            var desiredHeight = 400;

            // 可以先让base测量子控件
            var baseSize = base.MeasureOverride(availableSize);

            // 你可以根据 baseSize 做些计算
            // 这里简单：返回一个 (600, 400) 以内的大小即可
            return new Size(
                Math.Min(desiredWidth, availableSize.Width),
                Math.Min(desiredHeight, availableSize.Height)
            );
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // 给子控件安排布局
            var arranged = base.ArrangeOverride(finalSize);
            return arranged;
        }

        private static Image CreateImageControl(Bitmap bitmap)
        {
            var imageControl = new Image { Stretch = Stretch.Uniform, Source = bitmap, Margin = new Thickness(10) };
            imageControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            imageControl.Arrange(new Rect(imageControl.DesiredSize));
            return imageControl;
        }

        private bool TryExtractAnimateMotionAndColor(string svgXml, out AnimateInfo? info)
        {
            info = null;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(svgXml);

                // 1. 找 <animateMotion>
                var animateMotionNode = doc.GetElementsByTagName("animateMotion")
                    .Cast<XmlNode>()
                    .FirstOrDefault();

                if (animateMotionNode == null)
                    return false;

                var pathAttr = animateMotionNode.Attributes?["path"]?.Value;
                if (string.IsNullOrEmpty(pathAttr))
                    return false;

                var pathNode = doc.GetElementsByTagName("path")
                    .Cast<XmlNode>()
                    .FirstOrDefault();

                // 取 stroke
                var strokeAttr = pathNode?.Attributes?["stroke"]?.Value; // "#cd0000"
                // 取 stroke-width
                double.TryParse(pathNode?.Attributes?["stroke-width"]?.Value, out double strokeWidth);
                // 取 fill
                var fillAttr = pathNode?.Attributes?["fill"]?.Value;
                // dur="3s" 
                var durAttr = animateMotionNode.Attributes?["dur"]?.Value ?? "3s";
                double durSeconds = ParseDurToSeconds(durAttr);

                var repeatCountAttr = animateMotionNode.Attributes?["repeatCount"]?.Value ?? "indefinite";

                // 2. 找 <animate attributeName="fill"> 来解析颜色动画
                //    (假定只有一个, 你可自行扩展多个)
                var animateColorNode = doc.GetElementsByTagName("animate")
                    .Cast<XmlNode>()
                    .FirstOrDefault(n => n.Attributes?["attributeName"]?.Value == "fill");

                string? fromColor = null, toColor = null;
                double colorDurSeconds = durSeconds; // 如果没写dur,默认和 move动画时长一样
                string colorRepeat = repeatCountAttr;

                if (animateColorNode != null)
                {
                    fromColor = animateColorNode.Attributes?["from"]?.Value;
                    toColor = animateColorNode.Attributes?["to"]?.Value;

                    var durColor = animateColorNode.Attributes?["dur"]?.Value;
                    if (!string.IsNullOrEmpty(durColor))
                    {
                        colorDurSeconds = ParseDurToSeconds(durColor);
                    }

                    var repColor = animateColorNode.Attributes?["repeatCount"]?.Value;
                    if (!string.IsNullOrEmpty(repColor))
                    {
                        colorRepeat = repColor;
                    }
                }

                // 拿父节点 <text> 文字
                var textNode = animateMotionNode.ParentNode;
                string textContent = textNode?.InnerText?.Trim() ?? "SVG";

                info = new AnimateInfo
                {
                    Text = textContent,
                    PathData = pathAttr,
                    MoveDuration = durSeconds,
                    MoveRepeatCount = repeatCountAttr,
                    FromColor = fromColor,
                    ToColor = toColor,
                    ColorDuration = colorDurSeconds,
                    ColorRepeatCount = colorRepeat,
                    PathStroke = strokeAttr,
                    PathStrokeWidth = strokeWidth,
                    PathFill = fillAttr,
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private double ParseDurToSeconds(string durValue)
        {
            if (durValue.EndsWith("s"))
            {
                var numPart = durValue[..^1];
                if (double.TryParse(numPart, out double seconds))
                    return seconds;
            }

            return 3.0;
        }
    }


    public class AnimatedSvgTextControl : Control
    {
        private readonly string? _text;
        private readonly string? _pathData;

        // 移动动画
        private readonly double _moveDuration;
        private readonly string? _moveRepeatCount;

        // 颜色动画
        private readonly string? _fromColor;
        private readonly string? _toColor;
        private readonly double _colorDuration;
        private readonly string? _colorRepeatCount;

        // 路径数据
        private PathGeometry? _avaloniaPathGeo;
        private SKPath? _skPath;
        private float _totalLength;

        // 进度
        private double _moveProgress;
        private DispatcherTimer? _moveTimer;

        private double _colorProgress;
        private DispatcherTimer? _colorTimer;

        // 绘制文本
        private FormattedText? _formattedText;
        private SolidColorBrush _currentBrush = new(Colors.Red);
        private IBrush? _pathStrokeBrush;
        private double _pathStrokeThickness = 1.0;
        private IBrush? _pathFillBrush; // 可能暂时用不到

        public AnimatedSvgTextControl(AnimateInfo info)
        {
            _text = info.Text;
            _pathData = info.PathData;

            _moveDuration = info.MoveDuration;
            _moveRepeatCount = info.MoveRepeatCount;

            _fromColor = info.FromColor;
            _toColor = info.ToColor;
            _colorDuration = info.ColorDuration;
            _colorRepeatCount = info.ColorRepeatCount;
            if (!string.IsNullOrEmpty(info.PathStroke))
            {
                var c = ParseColor(info.PathStroke);
                if (c is { })
                    _pathStrokeBrush = new SolidColorBrush(c.Value);
            }

            _pathStrokeThickness = info.PathStrokeWidth;
            if (!string.IsNullOrEmpty(info.PathFill) && info.PathFill != "none")
            {
                var fc = ParseColor(info.PathFill);
                if (fc is { })
                    _pathFillBrush = new SolidColorBrush(fc.Value);
            }
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // 1) 解析路径
            if (!string.IsNullOrEmpty(_pathData))
            {
                // Avalonia 几何(仅用来画可视的Path)
                try
                {
                    _avaloniaPathGeo = PathGeometry.Parse(_pathData);
                }
                catch { }

                // Skia Path(用来测量长度 & 获取插值点)
                try
                {
                    _skPath = SKPath.ParseSvgPathData(_pathData);
                    if (_skPath != null)
                    {
                        using var measure = new SKPathMeasure(_skPath, false);
                        _totalLength = measure.Length;
                    }
                }
                catch { }
            }

            // 2) 生成文字
            if (!string.IsNullOrEmpty(_text))
            {
                _formattedText = new FormattedText(
                    _text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    40,
                    _currentBrush // 先用初始笔刷
                );
            }

            // 3) 启动移动动画
            if (_skPath != null && _totalLength > 0 && _moveDuration > 0)
            {
                _moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16.7) };
                _moveTimer.Tick += MoveTimerTick;
                _moveTimer.Start();
            }

            // 4) 启动颜色动画(若 from->to 都有值)
            if (!string.IsNullOrEmpty(_fromColor) && !string.IsNullOrEmpty(_toColor))
            {
                _currentBrush = new SolidColorBrush(ParseColor(_fromColor) ?? Colors.Red);

                _colorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16.7) };
                _colorTimer.Tick += ColorTimerTick;
                _colorTimer.Start();
            }
        }

        private void MoveTimerTick(object? sender, EventArgs e)
        {
            _moveProgress += 0.0167 / _moveDuration;
            if (_moveProgress > 1.0)
            {
                if (string.Equals(_moveRepeatCount, "indefinite", StringComparison.OrdinalIgnoreCase))
                {
                    _moveProgress = 0.0;
                }
                else
                {
                    _moveProgress = 1.0;
                    _moveTimer?.Stop();
                }
            }

            InvalidateVisual();
        }

        private void ColorTimerTick(object? sender, EventArgs e)
        {
            _colorProgress += 0.0167 / _colorDuration;
            if (_colorProgress > 1.0)
            {
                if (string.Equals(_colorRepeatCount, "indefinite", StringComparison.OrdinalIgnoreCase))
                {
                    _colorProgress = 0.0;
                }
                else
                {
                    _colorProgress = 1.0;
                    _colorTimer?.Stop();
                }
            }

            // 插值颜色
            if (!string.IsNullOrEmpty(_fromColor) && !string.IsNullOrEmpty(_toColor))
            {
                var c1 = ParseColor(_fromColor) ?? Colors.Red;
                var c2 = ParseColor(_toColor) ?? Colors.Blue;

                var lerped = LerpColor(c1, c2, (float)_colorProgress);
                _currentBrush.Color = lerped;
            }


            // 同步到 FormattedText 的前景色
            if (_formattedText != null)
            {
                // Avalonia 11 通常可以直接改 Foreground，但若不行就重新 new 一个
                _formattedText = new FormattedText(
                    _text ?? "SVG",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    40,
                    _currentBrush
                );
            }

            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            // 画路径(可视)
            if (_avaloniaPathGeo != null)
            {
                var pen = new Pen(Brushes.Gray, 2);
                context.DrawGeometry(null, pen, _avaloniaPathGeo);
            }

            // 画文字
            if (_formattedText == null || _skPath == null || _totalLength <= 0)
                return;

            float distance = (float)(_moveProgress * _totalLength);

            using var measure = new SKPathMeasure(_skPath, false);
            SKPoint position = default;
            SKPoint tangent = default;

            float currentLength = 0f;
            bool foundPos = false;
            do
            {
                float len = measure.Length;
                if (distance <= currentLength + len)
                {
                    float distInThisContour = distance - currentLength;
                    foundPos = measure.GetPositionAndTangent(distInThisContour, out position, out tangent);
                    break;
                }

                currentLength += len;
            } while (measure.NextContour());

            if (!foundPos)
                return;

            // 转换为 Avalonia 坐标
            var avaloniaPoint = new Point(position.X, position.Y);

            var offsetY = _formattedText.Height / 2;
            var correctedPoint = new Point(avaloniaPoint.X, avaloniaPoint.Y - offsetY);

            context.DrawText(_formattedText, correctedPoint);

            // if applied rotate transform, remember to pop it:
            // context.Pop();
            if (_avaloniaPathGeo != null)
            {
                var pen = new Pen(_pathStrokeBrush ?? Brushes.Gray, _pathStrokeThickness);
                // 如果真的需要 fill，可以用 _pathFillBrush，否则null
                context.DrawGeometry(_pathFillBrush, pen, _avaloniaPathGeo);
            }
        }

        // 解析颜色字符串 (#RRGGBB / #RGB / red / blue …)
        private Color? ParseColor(string colorStr)
        {
            try
            {
                // Avalonia 11 通用用法: Color.TryParse(string, out Color c)
                if (Color.TryParse(colorStr, out var c))
                    return c;
                // 再尝试一下 .NET 内置 KnownColors
                return (Color)new ColorConverter().ConvertFromString(colorStr)!;
            }
            catch
            {
                return null;
            }
        }

        // 线性插值颜色
        private static Color LerpColor(Color c1, Color c2, float t)
        {
            byte a = (byte)(c1.A + (c2.A - c1.A) * t);
            byte r = (byte)(c1.R + (c2.R - c1.R) * t);
            byte g = (byte)(c1.G + (c2.G - c1.G) * t);
            byte b = (byte)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(a, r, g, b);
        }
    }
}