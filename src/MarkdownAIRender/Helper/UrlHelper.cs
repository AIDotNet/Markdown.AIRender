using System.Diagnostics;

namespace MarkdownAIRender.Helper;

public class UrlHelper
{
    /// <summary>
    /// 使用默认浏览器打开指定链接
    /// </summary>
    /// <returns></returns>
    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    /// <summary>
    /// 打开指定Url
    /// </summary>
    /// <param name="uri"></param>
    public static void OpenUrlWithBrowser(Uri uri)
    {
        OpenUrl(uri.AbsoluteUri);
    }
}