namespace FocusTodo.App.Models;

public sealed class AppSetting
{
    public int Id { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public int DefaultSnoozeMinutes { get; set; } = 10;
    public string Theme { get; set; } = "Light";
    public bool ReminderSoundEnabled { get; set; } = true;
    public bool AutoCompleteParent { get; set; } = true;
    public bool ConfirmBeforeDelete { get; set; } = true;
}
