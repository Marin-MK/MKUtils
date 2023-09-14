using System.Diagnostics;
using System.Text;

namespace MKUtils;

public class Downloader
{
    public string URL { get; protected set; }
    public string? Filename { get; protected set; }
    public Stream Stream { get; protected set; }
    public bool Done { get; protected set; }
    public bool Cancelled { get; protected set; }
    public bool HadError { get; protected set; }
    public float Progress { get; protected set; }
    public bool CloseStream { get; set; } = false;

    public Action? OnFinished;
    public Action? OnCancelled;
    public Action<Exception>? OnError;

    public static string DownloadString(string url, TimeSpan? timeout = null, DynamicCallbackManager<DownloadProgress>? callbackManager = null, Encoding? encoding = null)
    {
        Logger.Instance?.WriteLine("Downloading to string...");
        MemoryStream stream = new MemoryStream();
        Downloader dl = new Downloader(url, stream);
        dl.OnError += ex => callbackManager?.OnError?.Invoke(ex);
        dl.Download(timeout, callbackManager);
        string text = (encoding ?? Encoding.Default).GetString(stream.ToArray());
        return text;
    }

    public static bool DownloadFile(string url, string Filename, TimeSpan? timeout = null, DynamicCallbackManager<DownloadProgress>? callbackManager = null)
    {
        Logger.Instance?.WriteLine("Downloading to file...");
        Downloader dl = new Downloader(url, Filename);
        dl.OnError += ex => callbackManager?.OnError?.Invoke(ex);
        dl.Download(timeout, callbackManager);
        return true;
    }

    public static Stream DownloadStream(string url, TimeSpan? timeout = null, DynamicCallbackManager<DownloadProgress>? callbackManager = null)
    {
        Logger.Instance?.WriteLine("Downloading to stream...");
        MemoryStream ms = new MemoryStream();
        Downloader dl = new Downloader(url, ms);
        dl.OnError += ex => callbackManager?.OnError?.Invoke(ex);
        dl.Download(timeout, callbackManager);
        ms.Position = 0;
        return ms;
    }

    public Downloader(string url, string filename) : this(url, new FileStream(filename, FileMode.Create, FileAccess.Write))
    {
        this.CloseStream = true;
        this.Filename = filename;
    }

    public Downloader(string url, Stream stream)
    {
        this.URL = url;
        this.Stream = stream;
    }

    public bool Download(TimeSpan? timeout = null, DynamicCallbackManager<DownloadProgress>? callbackManager = null, int BufferSize = 8192)
    {
        bool Ret = false;
        Thread thread = new Thread(() =>
        {
            long startTicks = Stopwatch.GetTimestamp();
            bool deleteFile = false;
            var client = new HttpClient();
            HttpResponseMessage response = null!;
            Stream content = null!;
            try
            {
                Logger.Instance?.WriteLine("Creating HTTP client...");
                client = new HttpClient();
                client.Timeout = timeout ?? TimeSpan.FromSeconds(10);
                Logger.Instance?.WriteLine($"Created HTTP client with {MKUtils.TimespanToString(client.Timeout)} timeout");
                Logger.Instance?.WriteLine($"Sending GET request to {URL}...");
                response = client.Send(new HttpRequestMessage(HttpMethod.Get, this.URL), HttpCompletionOption.ResponseHeadersRead);
                Logger.Instance?.WriteLine($"Got a response after {MKUtils.TimespanToString((TimeSpan) Logger.Instance?.TimeSinceLastWrite!)}.");
                if (Cancelled)
                {
                    OnCancelled?.Invoke();
                    client.Dispose();
                    response.Dispose();
                    return;
                }
                Logger.Instance?.WriteLine("Ensuring successful status code...");
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is null)
                {
                    Logger.Instance?.Error("No content was associated with the response; Content-Length is null.");
                    throw new HttpRequestException("Cannot download content when Content-Length is null.");
                }

                Logger.Instance?.WriteLine("Reading response content as stream");
                content = response.Content.ReadAsStream();
                if (Cancelled)
                {
                    OnCancelled?.Invoke();
                    client.Dispose();
                    response.Dispose();
                    content.Dispose();
                    return;
                }

                if (!string.IsNullOrEmpty(Filename))
                {
                    Logger.Instance?.WriteLine($"Writing to '{Filename}'; create any non-existing directories in the path we're writing to.");
                    string dirName = Path.GetDirectoryName(Filename)!;
                    if (!Directory.Exists(dirName))
                    {
                        Logger.Instance?.WriteLine($"Create directory '{dirName}'.");
                        Directory.CreateDirectory(dirName);
                    }
                }

                long totalBytes = (long)response.Content.Headers.ContentLength!;
                long totalRead = 0;
                byte[] buffer = new byte[BufferSize];
                bool reported1 = false;
                DownloadProgress progressObject = new DownloadProgress(0, totalBytes);
                Logger.Instance?.WriteLine($"Reading {progressObject.TotalBytesToString()} with buffer size of {buffer.Length}.");

                double lastReported = 0;
                while (totalRead < totalBytes)
                {
                    var bytesRead = content.Read(buffer, 0, buffer.Length);
                    if (Cancelled) break;
                    totalRead += bytesRead;
                    if (bytesRead == 0) break;
                    this.Stream.Write(buffer, 0, bytesRead);
                    deleteFile = true;
                    progressObject.BytesRead = totalRead;
                    if (progressObject.Factor - lastReported >= 0.05)
                    {
                        lastReported = progressObject.Factor;
                        Logger.Instance?.WriteLine($"Progress: {progressObject}");
                    }
                    callbackManager?.Update(progressObject);
                    if (bytesRead == totalBytes) reported1 = true;
                    if (Cancelled) break;
                }

                if (!Cancelled)
                {
                    Done = true;
                    deleteFile = false;
                    if (!reported1) callbackManager?.Update(new DownloadProgress(totalBytes, totalBytes));
                }

                callbackManager?.Stop();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is HttpRequestException || ex is TaskCanceledException || ex is UriFormatException || ex is NotSupportedException || ex is OperationCanceledException)
            {
                Logger.Instance?.Error("Downloader threw an exception: " + ex.Message + "\n" + ex.StackTrace);
                OnError?.Invoke(ex);
                HadError = true;
            }
            finally
            {
                client.Dispose();
                response?.Dispose();
                content?.Dispose();
                if (CloseStream) Stream?.Dispose();
                if (deleteFile && !string.IsNullOrEmpty(Filename) && File.Exists(Filename)) File.Delete(Filename);
            }
            if (Cancelled) OnCancelled?.Invoke();
            else
            {
                Logger.Instance?.WriteLine($"Download finished after {MKUtils.TimespanToString(Stopwatch.GetTimestamp() - startTicks)}.");
                OnFinished?.Invoke();
                Ret = true;
            }
        });
        thread.Start();
        while (thread.IsAlive)
        {
            callbackManager?.Idle();
        }
        return Ret && !HadError;
    }

    /// <summary>
    /// Stops the downloader and deletes the downloaded file if it was not fully downloaded.
    /// </summary>
    public void Cancel()
    {
        Logger.Instance?.WriteLine("Cancelling download...");
        if (Cancelled) throw new Exception("Downloader already cancelled.");
        Cancelled = true;
    }
}

public struct DownloadProgress : IProgressFactor
{
    internal static readonly (string Name, long Unit)[] ByteMagnitudes =
    {
        ("B",   (long) Math.Pow(1024, 0)),
        ("KiB", (long) Math.Pow(1024, 1)),
        ("MiB", (long) Math.Pow(1024, 2)),
        ("GiB", (long) Math.Pow(1024, 3)),
        ("TiB", (long) Math.Pow(1024, 4)),
        ("PiB", (long) Math.Pow(1024, 5)),
        ("EiB", (long) Math.Pow(1024, 6))
    };

    public long BytesRead;
    public long TotalBytes;
    public long BytesLeft => TotalBytes - BytesRead;
    public double Factor => (double) BytesRead / TotalBytes;
    public double Percentage => Factor * 100;

    public DownloadProgress(long bytesRead, long totalBytes)
    {
        BytesRead = bytesRead;
        TotalBytes = totalBytes;
    }

    public string ReadBytesToString()
    {
        return BytesToString(BytesRead);
    }

    public string TotalBytesToString()
    {
        return BytesToString(TotalBytes);
    }

    public static string BytesToString(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        for (int i = ByteMagnitudes.Length - 1; i >= 0; i--)
        {
            if (bytes >= ByteMagnitudes[i].Unit)
            {
                long units = bytes / ByteMagnitudes[i].Unit;
                return $"{units}.{double.Truncate(Math.Round((double) (bytes - units * ByteMagnitudes[i].Unit) / ByteMagnitudes[i].Unit, 2) * 100).ToString().TrimEnd('0').PadLeft(1, '0')} {ByteMagnitudes[i].Name}";
            }
        }
        return $"{bytes} B";
    }

    public override string ToString()
    {
        return $"{Math.Round(Percentage, 1)}% ({ReadBytesToString()} / {TotalBytesToString()})";
    }
}