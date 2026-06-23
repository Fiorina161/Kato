using System.Text;
using Spectre.Console;

namespace Kato;

/**
 * Entry point for a terminal-based task launcher that organizes commands into categories.
 * Provides a keyboard-driven interface to browse and execute configured tasks, while
 * maintaining a history of recently launched items for quick access.
 */
internal static class Program
{
    private const string CONFIG_FILE_NAME = "kato.ini";
    private const string RECENT_CATEGORY_NAME = "(Recent...)";

    /**
	 * Orchestrates the main application loop, managing user interaction between
	 * categories and tasks. Ensures configuration and history stay synchronized
	 * throughout the session, particularly when the user edits the config file.
	 */
    private static void Main()
    {
        Console.OutputEncoding = Console.InputEncoding = Encoding.UTF8;

        var historyManager = new RecentHistoryManager(maxRecent: 5);
        var configManager = new ConfigManager(CONFIG_FILE_NAME);
        var renderer = new UiRenderer();

        var config = TryLoad(configManager) ?? new Configuration([]);
        historyManager.Load(configManager.ConfigPath);
        historyManager.Prune(config);
        historyManager.Save(configManager.ConfigPath, out _);

        // Track selection state independently for each category so users
        // don't lose their position when switching between categories.
        var selectedCategory = 0;
        var inCategoryView = historyManager.Recent.Count == 0;
        var selectedTaskByCategory = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

        while (true)
        {
            var categories = BuildCategoryList(config, historyManager);

            if (categories.Count == 0)
            {
                if (HandleEmptyConfig(configManager, ref config, historyManager))
                    continue;
                break;
            }

            selectedCategory = Math.Clamp(selectedCategory, 0, categories.Count - 1);
            var categoryName = categories[selectedCategory];
            var tasks = BuildTaskList(categoryName, config, historyManager);

            selectedTaskByCategory.TryAdd(categoryName, 0);
            selectedTaskByCategory[categoryName] = Math.Clamp(selectedTaskByCategory[categoryName], 0, Math.Max(0, tasks.Count - 1));

            if (inCategoryView)
                renderer.Render(configManager.ConfigPath, categories, selectedCategory, null);
            else
                renderer.Render(configManager.ConfigPath, tasks.Select(t => t.DisplayName).ToList(), selectedTaskByCategory[categoryName], categoryName);

            var key = Console.ReadKey(intercept: true).Key;

            if (key == ConsoleKey.Escape)
            {
                if (inCategoryView) break;
                inCategoryView = true;
                continue;
            }

            if (key == ConsoleKey.E)
            {
                using var process = ProcessLauncher.OpenFileEditor(configManager.ConfigPath);
                if (process != null)
                    Reload(configManager, historyManager, ref config);
                continue;
            }

            if (key == ConsoleKey.R)
            {
                Reload(configManager, historyManager, ref config);
                continue;
            }

            if (key == ConsoleKey.UpArrow)
            {
                if (inCategoryView)
                    selectedCategory = (selectedCategory - 1 + categories.Count) % categories.Count;
                else if (tasks.Count > 0)
                    selectedTaskByCategory[categoryName] = (selectedTaskByCategory[categoryName] - 1 + tasks.Count) % tasks.Count;
                continue;
            }

            if (key == ConsoleKey.DownArrow)
            {
                if (inCategoryView)
                    selectedCategory = (selectedCategory + 1) % categories.Count;
                else if (tasks.Count > 0)
                    selectedTaskByCategory[categoryName] = (selectedTaskByCategory[categoryName] + 1) % tasks.Count;
                continue;
            }

            if (key == ConsoleKey.Enter)
            {
                if (inCategoryView)
                {
                    inCategoryView = false;
                    continue;
                }

                if (tasks.Count == 0)
                    continue;

                var item = tasks[selectedTaskByCategory[categoryName]];
                ProcessLauncher.Run(item.Command);
                historyManager.Add(item);
                historyManager.Prune(config);
                historyManager.Save(configManager.ConfigPath, out _);
                break;
            }
        }

        AnsiConsole.Clear();
    }

    /**
	 * Attempts to load configuration while providing graceful fallback behavior.
	 * Returns null on failure to allow the application to continue with an empty
	 * config rather than crashing, giving users a chance to fix the file.
	 */
    private static Configuration? TryLoad(ConfigManager manager)
    {
        try
        {
            return manager.Load();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to parse config:[/] {Markup.Escape(ex.Message)}");
            return null;
        }
    }

    /**
	 * Re-reads configuration from disk and synchronizes the history to reflect
	 * any changes. Used after the user edits the config file to bring those
	 * changes into the running application.
	 */
    private static void Reload(ConfigManager manager, RecentHistoryManager history, ref Configuration config)
    {
        try
        {
            config = manager.Load();
            history.Load(manager.ConfigPath);
            history.Prune(config);
            history.Save(manager.ConfigPath, out _);
        }
        catch { }
    }

    /**
	 * Provides a minimal interface when no categories are available, offering
	 * the user options to fix the config rather than immediately exiting.
	 * This prevents frustration from accidentally starting with an empty config file.
	 */
    private static bool HandleEmptyConfig(ConfigManager manager, ref Configuration config, RecentHistoryManager history)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[yellow]No categories found in config.[/]");
        AnsiConsole.MarkupLine("Press E to edit, Esc to quit.");

        var key = Console.ReadKey(intercept: true).Key;

        if (key == ConsoleKey.Escape)
            return false;

        if (key == ConsoleKey.E)
            using (var process = ProcessLauncher.OpenFileEditor(manager.ConfigPath))
                if (process != null)
                    Reload(manager, history, ref config);

        return true;
    }

    /**
	 * Builds the list of categories with "Recent..." at the top when applicable.
	 * This ordering prioritizes frequently-used items while keeping the config
	 * categories alphabetically sorted for predictability.
	 */
    private static List<string> BuildCategoryList(Configuration config, RecentHistoryManager history)
    {
        var list = new List<string>();

        if (history.Recent.Count > 0)
            list.Add(RECENT_CATEGORY_NAME);

        list.AddRange(config.Sections.Keys);

        return list;
    }

    /**
	 * Constructs the task list for a given category, with special handling for
	 * "Recent..." which pulls from history and validates items still exist in config.
	 * Regular categories display tasks sorted alphabetically for consistency.
	 */
    private static List<LaunchItem> BuildTaskList(string categoryName, Configuration config, RecentHistoryManager history)
    {
        if (categoryName.Equals(RECENT_CATEGORY_NAME, StringComparison.CurrentCultureIgnoreCase))
        {
            // Filter out recent items whose tasks have been removed from config,
            // preventing errors when users clean up their configuration.
            return history.Recent.OrderByDescending(r => r.LaunchedAt)
                .Where(r => config.Sections.TryGetValue(r.Section, out var tasks) && tasks.FirstOrDefault(t => t.Name.Equals(r.Key, StringComparison.CurrentCultureIgnoreCase)) is not null)
                .Select(r => new LaunchItem($"{r.Section} [dim]-[/] {r.Key}", r.Key, config.Sections[r.Section].First(t => t.Name.Equals(r.Key, StringComparison.CurrentCultureIgnoreCase)).Command, r.Section))
                .ToList();
        }

        return config.Sections.TryGetValue(categoryName, out var taskList)
            ? taskList.Select(t => new LaunchItem(t.Name, t.Name, t.Command, categoryName)).ToList()
            : [];
    }
}