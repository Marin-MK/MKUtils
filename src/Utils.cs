using System.Diagnostics;

namespace MKUtils;

public static class MKUtils
{
    public static string ProgramFilesPath
    {
        get
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                if (Directory.Exists("/usr/local/bin")) folder = "/usr/local/bin";
                else
                {
                    folder = Environment.GetFolderPath(Environment.SpecialFolder.Personal).Replace('\\', '/');
                    if (string.IsNullOrEmpty(folder))
                    {
                        if (Directory.Exists("/opt")) folder = "/opt";
                    }
                }
            }
            return folder;
        }
    }

	public static string AppDataFolder => odl.Graphics.Platform switch
	{
		odl.Platform.Windows => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace('\\', '/'),
		odl.Platform.Linux => new Func<string>(() =>
		{
			string sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");
			if (!string.IsNullOrEmpty(sudoUser)) return $"/home/{sudoUser}";
			else return Environment.GetEnvironmentVariable("HOME");
		})(),
		_ => throw new NotImplementedException()
	};

	public static readonly (string Name, long Unit)[] ByteMagnitudes =
    {
        ("B", (long) Math.Pow(1024, 0)),
        ("KiB", (long) Math.Pow(1024, 1)),
        ("MiB", (long) Math.Pow(1024, 2)),
        ("GiB", (long) Math.Pow(1024, 3)),
        ("TiB", (long) Math.Pow(1024, 4)),
        ("PiB", (long) Math.Pow(1024, 5)),
        ("EiB", (long) Math.Pow(1024, 6))
    };

    public static string BytesToString(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        for (int i = ByteMagnitudes.Length - 1; i >= 0; i--)
        {
            if (bytes >= ByteMagnitudes[i].Unit)
            {
                long units = bytes / ByteMagnitudes[i].Unit;
                return $"{units}.{double.Truncate(Math.Round((double)(bytes - units * ByteMagnitudes[i].Unit) / ByteMagnitudes[i].Unit, 2) * 100).ToString().TrimEnd('0').PadLeft(1, '0')} {ByteMagnitudes[i].Name}";
            }
        }
        return $"{bytes} B";
    }

    public static string TimespanToString(TimeSpan timeSpan, bool Brief = true, bool WithMilliseconds = true, bool WithMicroseconds = false) => TimespanToString(timeSpan.Ticks, Brief, WithMilliseconds, WithMicroseconds);

    public static string TimespanToString(long ticks, bool Brief = true, bool WithMilliseconds = true, bool WithMicroseconds = false)
    {
        string result = "";
        if (ticks >= 10_000L * 1000 * 60 * 60 * 24)
        {
            long units = ticks / 10_000L / 60 / 60 / 24;
            ticks -= units * 10_000L * 60 * 60 * 24;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "d" : ("day" + (units > 1 ? "s" : ""))) + " ";
        }
        if (ticks >= 10_000L * 1000 * 60 * 60)
        {
            long units = ticks / 10_000L / 60 / 60;
            ticks -= units * 10_000L * 60 * 60;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "h" : ("hour" + (units > 1 ? "s" : ""))) + " ";
        }
        if (ticks >= 10_000L * 1000 * 60)
        {
            long units = ticks / 10_000L / 1000 / 60;
            ticks -= units * 10_000L * 1000 * 60;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "min" : ("minute" + (units > 1 ? "s" : ""))) + " ";
        }
        if (ticks >= 10_000L * 1000)
        {
            long units = ticks / 10_000L / 1000;
            ticks -= units * 10_000L * 1000;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "s" : ("second" + (units > 1 ? "s" : ""))) + " ";
        }
        if (WithMilliseconds && ticks >= 10_000L)
        {
            long units = ticks / 10_000L;
            ticks -= units * 10_000L;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "ms" : ("millisecond" + (units > 1 ? "s" : ""))) + " ";
        }
        if (WithMicroseconds && ticks >= 10)
        {
            long units = ticks / 10;
            result += units.ToString();
            if (!Brief) result += " ";
            result += (Brief ? "μs" : ("microsecond" + (units > 1 ? "s" : ""))) + " ";
        }
        return result.TrimEnd();
    }

	public static string TrimVersion(string version)
	{
		List<string> _split = version.Trim('\n', '\r', ' ').Split('.').ToList();
		while (_split[^1] == "0")
		{
			_split.RemoveAt(_split.Count - 1);
            if (_split.Count == 0) return "0";
		}
		if (_split.Count == 0) _split.Add("1");
		return _split.GetRange(0, _split.Count).Aggregate((a, b) => a + "." + b);
	}

    public static void MakeNonRoot(string path)
    {
		Process p = new Process();
		p.StartInfo = new ProcessStartInfo("chmod");
		p.StartInfo.ArgumentList.Add("0777");
		p.StartInfo.ArgumentList.Add(path.Replace('\\', '/'));
		p.StartInfo.UseShellExecute = false;
		p.StartInfo.CreateNoWindow = true;
		p.Start();
	}
}
