namespace MKUtils;

public class Logger : odl.ILogger
{
    public static Logger Instance;
    private StreamWriter? Log;

    public DateTime? LastWrite;
    public TimeSpan TimeSinceLastWrite => DateTime.Now - (DateTime) LastWrite!;

    public void Start(bool autoFlush = true)
    {
        if (Log != null) throw new InvalidLogException("Existing log is still active!");
        Log = new StreamWriter(Console.OpenStandardOutput());
        Log.AutoFlush = autoFlush;
        WriteLine("--- Log initialized ---");
    }

    public void ForceRedirectStdOutErr()
    {
        Console.SetOut(Log);
        Console.SetError(Log);
    }

    public void Start(StreamWriter logger, bool autoFlush = true)
    {
        if (Log != null) throw new InvalidLogException("Existing log is still active!");
        Log = logger;
        Log.AutoFlush = autoFlush;
        WriteLine("--- Log initialized ---");
    }

    public void Start(string filename, bool autoFlush = true)
    {
        if (Log != null) throw new InvalidLogException("Existing log is still active!");
		Log = new StreamWriter(new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.Read));
        Log.AutoFlush = autoFlush;
        WriteLine("--- Log initialized ---");
    }

    bool firstConsecutiveWrite = true;

    public void Write(string message, params object[] args)
    {
        if (firstConsecutiveWrite) WritePrefix(null, message, args);
        else if (args.Length > 0) Log.Write(string.Format(message.Replace("\r", ""), args));
        else Log.Write(message.Replace("\r", ""));
        firstConsecutiveWrite = false;
    }

    public void WriteLine()
    {
        Log.WriteLine();
    }

    public void WriteLine(string message, params object[] args)
    {
        WritePrefix(null, message + "\n", args);
    }

    private void WritePrefix(string? prefix, string message, params object[] args)
    {
        firstConsecutiveWrite = true;
        if (Log == null) throw new InvalidLogException();
        LastWrite = DateTime.Now;
        string meta = LastWrite.Value.ToString("HH:mm:ss") + "." + LastWrite.Value.Millisecond.ToString().PadLeft(3, '0');
        message = message.Replace("\r", "");
        if (args.Length > 0) message = string.Format(message, args);
        if (string.IsNullOrEmpty(prefix)) prefix = "[" + meta + "] ";
        else prefix = prefix + " [" + meta + "] ";
        List<string> lines = message.Split(new char[] { '\r', '\n' }).ToList();
        Log.Write(prefix);
        for (int i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrEmpty(lines[i]) && i == lines.Count - 1) continue;
            if (i > 0) Log.Write(new string(' ', prefix.Length) + lines[i]);
            else Log.Write(lines[i]);
            if (i != lines.Count - 1) Log.WriteLine();
        }
    }

    public void Error(string message, params object[] args)
    {
        WritePrefix("ERROR", message + "\n", args);
    }

    public void Error(Exception ex)
    {
        WritePrefix("ERROR", ex.Message + "\n" + ex.StackTrace + "\n");
    }

    public void Warn(string message, params object[] args)
    {
        WritePrefix("WARNING", message + "\n", args);
    }

    public void Stop()
    {
        if (Log == null) throw new InvalidLogException();
        WriteLine("--- Log stopped ---");
        Log!.Close();
        Log = null;
    }
}

public class InvalidLogException : Exception
{
    public InvalidLogException(string message = "No logs are currently active!") : base(message) { }
}