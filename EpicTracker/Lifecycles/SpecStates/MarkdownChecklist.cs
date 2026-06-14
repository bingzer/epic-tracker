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
                result.Add(new ChecklistItem(ExtractName(trimmed[6..]), IsChecked: true));
            }
            else if (trimmed.StartsWith("- [ ] ") || trimmed.StartsWith("- [-] "))
            {
                result.Add(new ChecklistItem(ExtractName(trimmed[6..]), IsChecked: false));
            }
        }

        return result;
    }

    private static string ExtractName(string text)
    {
        var trimmed = text.Trim();
        var spaceIdx = trimmed.IndexOfAny([' ', '\t']);
        return spaceIdx < 0 ? trimmed : trimmed[..spaceIdx];
    }
}
