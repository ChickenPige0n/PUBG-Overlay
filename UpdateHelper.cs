using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace PubgOverlay;
public class UpdateHelper
{
    [SuppressMessage("Usage", "CA2211:非常量字段应当不可见")]
    public static Version CurrentVersion = new(1, 7, 1, 0);

    public static async void Update(string url, IProgress<float> progress)
    {
        try
        {
            using var client = new HttpClient();
            var fileName = Path.GetFileName(url);
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            // 删除旧的临时文件（如果有）
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            
            // 下载文件
            var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await client.DownloadAsync(url, fs, progress);
            await fs.DisposeAsync();

            Console.WriteLine($"Downloaded {fileName} to {tempPath}");

            // 解压文件
            var extractPath = Path.Combine(Path.GetTempPath(), "PUBG-Overlay");
            var selfProcess = Process.GetCurrentProcess();
            var destPath = Path.GetDirectoryName(Environment.ProcessPath);
            Console.WriteLine($"destPath: {destPath}");
            if (Directory.Exists(extractPath))
            {
                Directory.Delete(extractPath, true);
            }

            await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(tempPath, extractPath));
            File.Delete(tempPath);

            Console.WriteLine($"Extracted {fileName} to {extractPath}");

            // 写入替换脚本（一次性写完）
            var scriptPath = Path.Combine(extractPath, "replace.bat");
            // ReSharper disable once StringLiteralTypo
            var scriptContent = $"""
                                 @echo off
                                 setlocal enableextensions
                                 setlocal enableDelayedExpansion


                                 set "ProcessName={selfProcess.ProcessName}"  REM 替换为你要等待的进程名称
                                 :WaitForProcess
                                 tasklist /FI "IMAGENAME eq %ProcessName%" 2>nul | find /I "%ProcessName%" >nul
                                 if "%ERRORLEVEL%"=="0" (
                                     echo 等待进程 "%ProcessName%" 结束中...
                                     timeout /T 1 >nul
                                     goto WaitForProcess
                                 )



                                 :: 设置源目录和目标目录
                                 set "src={extractPath}"
                                 set "dst={destPath}"

                                 :: 复制目录中的所有文件和子目录到目标目录
                                 robocopy "!src!" "!dst!" /s /e
                                 if errorlevel 8 echo 复制失败！请检查源目录和目标目录是否存在！ & pause & exit /b


                                 :: 删除脚本本身（可选）
                                 del "%~f0"

                                 echo 操作完成！
                                 pause
                                 exit

                                 """;

            await File.WriteAllTextAsync(scriptPath, scriptContent);
            Console.WriteLine($"Wrote replace script to {scriptPath}");

            // 运行脚本
            var process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.Arguments = $"/c \"{scriptPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }
}

public static class HttpClientExtensions
{
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination,
        IProgress<float> progress, CancellationToken cancellationToken = default)
    {
        // Get the http headers first to examine the content length
        using var response =
            await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var contentLength = response.Content.Headers.ContentLength;

        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken);
            return;
        }

        // Convert absolute progress (bytes downloaded) into relative progress (0% - 100%)
        var relativeProgress =
            new Progress<long>(totalBytes => { progress.Report((float)totalBytes / contentLength.Value); });
        // Use extension method to report progress while downloading
        await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
        progress.Report(1);
    }
}

public static class StreamExtensions
{
    public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize,
        IProgress<long> progress, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);
        if (!source.CanRead)
            throw new ArgumentException("Has to be readable", nameof(source));
        if (!destination.CanWrite)
            throw new ArgumentException("Has to be writable", nameof(destination));

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }
}