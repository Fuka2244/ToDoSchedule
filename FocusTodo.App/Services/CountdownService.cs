using FocusTodo.App.Models;

namespace FocusTodo.App.Services;

public sealed class CountdownService(ILocalizationService? localizationService = null) : ICountdownService
{
    public string GetCountdownText(TodoItem item, DateTime now)
    {
        if (item.IsCompleted)
        {
            return Text("Done");
        }

        if (item.DueAt is null)
        {
            return Text("NoDdl");
        }

        var due = item.DueAt.Value;
        var remaining = due - now;
        if (remaining < TimeSpan.Zero)
        {
            var overdue = now - due;
            return Format("OverdueText", Math.Max(0, overdue.Days), overdue.Hours);
        }

        if (remaining.TotalDays > 7)
        {
            return Format("SoonDays", Math.Ceiling(remaining.TotalDays));
        }

        if (remaining.TotalDays >= 1)
        {
            return Format("SoonDaysHours", remaining.Days, remaining.Hours);
        }

        if (remaining.TotalHours >= 1)
        {
            return Format("SoonHoursMinutes", (int)remaining.TotalHours, remaining.Minutes);
        }

        return Format("SoonMinutes", Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)));
    }

    public string GetDueState(TodoItem item, DateTime now)
    {
        if (item.IsCompleted)
        {
            return "Done";
        }

        if (item.DueAt is null)
        {
            return "Normal";
        }

        if (item.DueAt.Value < now)
        {
            return "Overdue";
        }

        if (item.DueAt.Value.Date == now.Date)
        {
            return "Today";
        }

        if (item.DueAt.Value - now <= TimeSpan.FromDays(2))
        {
            return "Soon";
        }

        return "Normal";
    }

    private string Text(string key)
    {
        return localizationService?.Get(key) ?? key switch
        {
            "Done" => "已完成",
            "NoDdl" => "无 DDL",
            _ => key
        };
    }

    private string Format(string key, params object[] values)
    {
        var template = localizationService?.Get(key) ?? key switch
        {
            "OverdueText" => "已逾期 {0} 天 {1} 小时",
            "SoonDays" => "还剩 {0} 天",
            "SoonDaysHours" => "还剩 {0} 天 {1} 小时",
            "SoonHoursMinutes" => "还剩 {0} 小时 {1} 分钟",
            "SoonMinutes" => "还剩 {0} 分钟",
            _ => key
        };
        return string.Format(template, values);
    }
}
