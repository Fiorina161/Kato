using Spectre.Console;

namespace Kato;

/**
 * Responsible for rendering the terminal UI for the task picker application.
 * Uses Spectre.Console to create a single-list interface that drills from
 * categories into tasks and back.
 */
internal sealed class UiRenderer
{
	public void Render(string configPath, List<string> items, int selectedItem, string? categoryName)
	{
		AnsiConsole.Clear();

		var configRow = new Table()
			.HideHeaders()
			.Border(TableBorder.None)
			.Expand()
			.AddColumn(new TableColumn(string.Empty).RightAligned());
		configRow.AddRow($"[dim]{Markup.Escape(ToUnixPath(configPath))}[/]");
		AnsiConsole.Write(configRow);

		AnsiConsole.Write(new FigletText("Kato").Color(Color.Yellow));

		AnsiConsole.WriteLine();

		if (categoryName is not null)
			AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(categoryName)}[/]");

		AnsiConsole.MarkupLine(BuildList(items, selectedItem));

		AnsiConsole.WriteLine();
		AnsiConsole.MarkupLine("[dim]E edit • R reload[/]");
	}

	/**
	 * Converts a Windows-style path to a Unix-style path for consistent display in the terminal.
	 */
	private static string ToUnixPath(string path)
	{
		return "/" + path[(Path.GetPathRoot(path)?.Length ?? 0)..].Replace('\\', '/');
	}

	private static string BuildList(List<string> items, int selected)
	{
		if (items.Count == 0)
			return "  [green](empty)[/]";

		return string.Join(Environment.NewLine, items.Select((item, i) => i == selected
			? $"  [black on yellow]{item}[/]"
			: $"  {item}"));
	}
}