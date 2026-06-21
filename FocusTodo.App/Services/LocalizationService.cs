namespace FocusTodo.App.Services;

public sealed class LocalizationService(IAppPreferencesService preferencesService) : ILocalizationService
{
    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["zh-CN"] = new Dictionary<string, string>
        {
            ["Add"] = "添加",
            ["AddBigTodo"] = "添加大任务",
            ["AddSmallTodo"] = "添加小任务",
            ["All"] = "全部",
            ["AutoCreateNextRecurringTodos"] = "完成循环任务后自动生成下一次",
            ["Clear"] = "清除",
            ["Close"] = "关闭",
            ["CloseToTrayHint"] = "关闭主窗口会隐藏到托盘。请使用托盘 Exit 完全退出。",
            ["Completed"] = "已完成",
            ["Count"] = "次数",
            ["DatabaseLocation"] = "数据库存储目录",
            ["Ddl"] = "DDL",
            ["DdlDate"] = "DDL 日期",
            ["DdlTime"] = "DDL 时间",
            ["Delete"] = "删除",
            ["DeleteTask"] = "删除任务",
            ["DeleteTaskConfirm"] = "删除 \"{0}\"？",
            ["Description"] = "描述",
            ["Done"] = "已完成",
            ["Edit"] = "编辑",
            ["EnableReminder"] = "启用提醒",
            ["Every"] = "每",
            ["Future"] = "未来",
            ["Interval"] = "间隔",
            ["Language"] = "语言",
            ["Lead"] = "提前",
            ["LoadedTodos"] = "已加载 {0} 个大任务",
            ["NewSmallTodo"] = "新的小任务",
            ["NewTodoTitle"] = "新任务标题",
            ["NoDdl"] = "无 DDL",
            ["OperationFailed"] = "操作失败",
            ["Overdue"] = "逾期",
            ["OverdueText"] = "已逾期 {0} 天 {1} 小时",
            ["PinWindow"] = "置顶窗口",
            ["Priority"] = "优先级",
            ["Ready"] = "就绪",
            ["Repeat"] = "循环",
            ["RestartRequired"] = "设置已保存。数据库目录变更将在重启后生效。",
            ["Save"] = "保存",
            ["SearchByTitle"] = "按标题搜索",
            ["Settings"] = "设置",
            ["SnoozeNextReminder"] = "稍后提醒",
            ["SoonDays"] = "还剩 {0} 天",
            ["SoonDaysHours"] = "还剩 {0} 天 {1} 小时",
            ["SoonHoursMinutes"] = "还剩 {0} 小时 {1} 分钟",
            ["SoonMinutes"] = "还剩 {0} 分钟",
            ["TaskDeleted"] = "任务已删除",
            ["TaskDetail"] = "任务详情",
            ["Title"] = "标题",
            ["Today"] = "今天",
            ["Top"] = "置顶",
            ["Lock"] = "锁定",
            ["ShowDone"] = "显示完成",
            ["Opacity"] = "透明度",
            ["OpenFocusTodo"] = "打开 FocusTodo",
            ["PauseReminders"] = "暂停提醒 1 小时",
            ["QuickAddTodo"] = "快速添加 Todo",
            ["ReminderCount"] = "提醒 {0}/{1}",
            ["ReminderTitle"] = "FocusTodo 提醒",
            ["ResumeReminders"] = "恢复提醒",
            ["TestNotification"] = "测试通知",
            ["Unpin"] = "取消固定"
        },
        ["en-US"] = new Dictionary<string, string>
        {
            ["Add"] = "Add",
            ["AddBigTodo"] = "Add Big Todo",
            ["AddSmallTodo"] = "Add Small Todo",
            ["All"] = "All",
            ["AutoCreateNextRecurringTodos"] = "Auto-create next recurring todo when completed",
            ["Clear"] = "Clear",
            ["Close"] = "Close",
            ["CloseToTrayHint"] = "Close hides to tray. Use tray Exit to quit.",
            ["Completed"] = "Completed",
            ["Count"] = "Count",
            ["DatabaseLocation"] = "Database storage folder",
            ["Ddl"] = "DDL",
            ["DdlDate"] = "DDL Date",
            ["DdlTime"] = "DDL Time",
            ["Delete"] = "Delete",
            ["DeleteTask"] = "Delete task",
            ["DeleteTaskConfirm"] = "Delete \"{0}\"?",
            ["Description"] = "Description",
            ["Done"] = "Done",
            ["Edit"] = "Edit",
            ["EnableReminder"] = "Enable reminder",
            ["Every"] = "every",
            ["Future"] = "Future",
            ["Interval"] = "Interval",
            ["Language"] = "Language",
            ["Lead"] = "Lead",
            ["LoadedTodos"] = "Loaded {0} big todos",
            ["NewSmallTodo"] = "New small todo",
            ["NewTodoTitle"] = "New Todo Title",
            ["NoDdl"] = "No DDL",
            ["OperationFailed"] = "Operation failed",
            ["Overdue"] = "Overdue",
            ["OverdueText"] = "Overdue {0} days {1} hours",
            ["PinWindow"] = "Pin Window",
            ["Priority"] = "Priority",
            ["Ready"] = "Ready",
            ["Repeat"] = "Repeat",
            ["RestartRequired"] = "Settings saved. Database folder changes take effect after restart.",
            ["Save"] = "Save",
            ["SearchByTitle"] = "Search by title",
            ["Settings"] = "Settings",
            ["SnoozeNextReminder"] = "Snooze next reminder",
            ["SoonDays"] = "{0} days left",
            ["SoonDaysHours"] = "{0} days {1} hours left",
            ["SoonHoursMinutes"] = "{0} hours {1} minutes left",
            ["SoonMinutes"] = "{0} minutes left",
            ["TaskDeleted"] = "Task deleted",
            ["TaskDetail"] = "Task Detail",
            ["Title"] = "Title",
            ["Today"] = "Today",
            ["Top"] = "Top",
            ["Lock"] = "Lock",
            ["ShowDone"] = "Show done",
            ["Opacity"] = "Opacity",
            ["OpenFocusTodo"] = "Open FocusTodo",
            ["PauseReminders"] = "Pause reminders 1 hour",
            ["QuickAddTodo"] = "Quick Add Todo",
            ["ReminderCount"] = "Reminder {0}/{1}",
            ["ReminderTitle"] = "FocusTodo reminder",
            ["ResumeReminders"] = "Resume reminders",
            ["TestNotification"] = "Test notification",
            ["Unpin"] = "Unpin"
        }
    };

    public event EventHandler? LanguageChanged;
    public IReadOnlyList<string> LanguageOptions { get; } = ["zh-CN", "en-US"];
    public string CurrentLanguage => preferencesService.Preferences.Language;

    public string Get(string key)
    {
        var language = Texts.ContainsKey(CurrentLanguage) ? CurrentLanguage : "zh-CN";
        if (Texts[language].TryGetValue(key, out var value))
        {
            return value;
        }

        return Texts["en-US"].TryGetValue(key, out var fallback) ? fallback : key;
    }

    public void SetLanguage(string language)
    {
        var normalized = LanguageOptions.Contains(language) ? language : "zh-CN";
        if (preferencesService.Preferences.Language == normalized)
        {
            return;
        }

        preferencesService.Preferences.Language = normalized;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
}
