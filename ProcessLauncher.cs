using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Kato;

internal static class ProcessLauncher
{
	public static Process? Run(string command)
	{
		var startInfo = CreateShellStartInfo(command);

		return Process.Start(startInfo) ?? throw new Exception("Unable to launch process");
	}

	public static Process? OpenFileEditor(string path)
	{
		var startInfo = CreateOpenFileStartInfo(path);
		return Process.Start(startInfo);
	}

	private static ProcessStartInfo CreateShellStartInfo(string command)
	{
		var info = new ProcessStartInfo();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			info.FileName = "cmd.exe";
			info.Arguments = $"/S /C \"{command}\"";
			info.CreateNoWindow = true;
			return info;
		}

		info.FileName = "/bin/sh";
		info.ArgumentList.Add("-c");
		info.ArgumentList.Add(command);
		return info;
	}

	private static ProcessStartInfo CreateOpenFileStartInfo(string path)
	{
		var info = new ProcessStartInfo();

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			info.FileName = "cmd.exe";
			info.ArgumentList.Add("/C");
			info.ArgumentList.Add("start");
			info.ArgumentList.Add(string.Empty); // window title required by `start`
			info.ArgumentList.Add("/wait");
			info.ArgumentList.Add(path);
			return info;
		}

		if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			info.FileName = "open";
			info.ArgumentList.Add("-W");
			info.ArgumentList.Add(path);
			return info;
		}

		info.FileName = Environment.GetEnvironmentVariable("VISUAL")
			?? Environment.GetEnvironmentVariable("EDITOR")
			?? "vi";
		info.ArgumentList.Add(path);
		return info;
	}
}