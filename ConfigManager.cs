namespace Kato;

/**
 * Manages configuration file discovery, creation, and parsing for the task runner.
 * Searches upward from the current directory to support project-level configuration,
 * similar to how .git directories or .editorconfig files are discovered.
 */
internal sealed class ConfigManager(string filename)
{
	private const string DEFAULT_TEMPLATE = """
	                                       ; These are example tasks. Edit or remove them as you see fit.
	                                       [Build]
	                                       Restore=dotnet restore
	                                       Build=dotnet build
	                                       Test=dotnet test

	                                       [Run]
	                                       Api=dotnet run --project src/MyApi
	                                       Ui=npm --prefix src/ui start
	                                       """;

	public readonly string ConfigPath = FindConfigPath(filename) ?? CreateDefaultConfig(filename);

	/**
	 * Parses the configuration file into a structured format.
	 */
	public Configuration Load()
	{
		var sections = new Dictionary<string, List<TaskItem>>(StringComparer.CurrentCultureIgnoreCase);
		string? currentSection = null;

		foreach (var (line, i) in File.ReadAllLines(ConfigPath).Select((l, i) => (l, i)))
		{
			var trimmed = line.Trim();

			// Allow blank lines and comments for readability and documentation within the config file
			if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
				continue;

			// Section headers group related tasks, making large config files easier to navigate
			if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
			{
				currentSection = trimmed[1..^1].Trim();
				if (currentSection.Length == 0)
					throw new FormatException($"Empty section at line {i + 1}");
				sections.TryAdd(currentSection, []);
				continue;
			}

			if (trimmed.StartsWith("--"))
				continue;

			// Parse task definition: validate format and reject malformed entries early
			var eq = trimmed.IndexOf('=');
			if (eq <= 0)
				throw new FormatException($"Expected key=value at line {i + 1}");

			// Require tasks to be within sections to maintain organization and prevent ambiguity
			if (currentSection is null)
				throw new FormatException($"Task outside section at line {i + 1}");

			var name = trimmed[..eq].Trim();
			var command = trimmed[(eq + 1)..].Trim();
			if (name.Length == 0 || command.Length == 0)
				throw new FormatException($"Invalid task at line {i + 1}");

			sections[currentSection].Add(new TaskItem(name, command));
		}

		return new Configuration(sections);
	}

	/**
	 * Walks up the directory hierarchy to find the config file.
	 */
	private static string? FindConfigPath(string fileName)
	{
		for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
		{
			var candidate = Path.Combine(dir.FullName, fileName);
			if (File.Exists(candidate))
				return candidate;
		}
		return null;
	}

	/**
	 * Creates a default config in the current directory when none exists.
	 */
	private static string CreateDefaultConfig(string fileName)
	{
		var path = Path.Combine(Environment.CurrentDirectory, fileName);
		File.WriteAllText(path, DEFAULT_TEMPLATE);
		return path;
	}
}