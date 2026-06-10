namespace EpicTracker.Lifecycles.SpecStates;

internal record ChecklistItem(string Name, bool IsChecked);

internal static class MarkdownChecklist
{
    public static IReadOnlyList<ChecklistItem> Parse(string markdown, string sectionHeader)
    {
        var result = new List<ChecklistItem>();
        var inSection = false;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimEnd();

            if (trimmed == sectionHeader)
            {
                inSection = true;
                continue;
            }

            if (trimmed.StartsWith("## ") && inSection)
            {
                inSection = false;
            }

            if (!inSection)
            {
                continue;
            }

            if (trimmed.StartsWith("- [x] ") || trimmed.StartsWith("- [X] "))
            {
                result.Add(new ChecklistItem(trimmed[6..].Trim(), IsChecked: true));
            }
            else if (trimmed.StartsWith("- [ ] "))
            {
                result.Add(new ChecklistItem(trimmed[6..].Trim(), IsChecked: false));
            }
        }

        return result;
    }
}
