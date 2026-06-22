using FocusTodo.App.Data;
using FocusTodo.App.Models;
using FocusTodo.App.Repositories;
using FocusTodo.App.Services;
using FocusTodo.App.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FocusTodo.Tests;

public sealed class TodoServiceTests
{
    [Fact]
    public async Task CreateRootTodo_PersistsTask()
    {
        await using var fixture = await TestFixture.CreateAsync();

        var todo = await fixture.Service.CreateRootTodoAsync("Write phase one", DateTime.Today.AddHours(18), TodoPriority.High);

        var saved = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == todo.Id);
        Assert.Equal("Write phase one", saved.Title);
        Assert.Equal(TodoPriority.High, saved.Priority);
        Assert.NotNull(saved.DueAt);
    }

    [Fact]
    public async Task CreateRootTodo_PersistsRecurrence()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var dueAt = new DateTime(2026, 6, 22, 18, 0, 0);

        var todo = await fixture.Service.CreateRootTodoAsync("Daily ddl", dueAt, TodoPriority.High, RepeatType.Daily, 1);

        var saved = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == todo.Id);
        Assert.Equal(RepeatType.Daily, saved.RepeatType);
        Assert.Equal(1, saved.RepeatInterval);
        Assert.True(saved.ReminderEnabled);
        Assert.Equal(0, saved.ReminderLeadMinutes);
        Assert.Equal(1, saved.ReminderMaxCount);
    }

    [Fact]
    public async Task CreateChildTodo_PersistsUnderParent()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var parent = await fixture.Service.CreateRootTodoAsync("Big Todo", null, TodoPriority.Normal);

        var child = await fixture.Service.CreateChildTodoAsync(parent.Id, "Small Todo", null, TodoPriority.Low);

        var savedChild = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == child.Id);
        Assert.Equal(parent.Id, savedChild.ParentId);
    }

    [Fact]
    public async Task CompletingAllChildren_AutoCompletesParent()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var parent = await fixture.Service.CreateRootTodoAsync("Project", null, TodoPriority.Normal);
        var child = await fixture.Service.CreateChildTodoAsync(parent.Id, "Step", null, TodoPriority.Normal);

        await fixture.Service.ToggleCompletedAsync(child.Id, true);

        var savedParent = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == parent.Id);
        Assert.True(savedParent.IsCompleted);
        Assert.NotNull(savedParent.CompletedAt);
    }

    [Fact]
    public async Task RestoringChild_RestoresParent()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var parent = await fixture.Service.CreateRootTodoAsync("Project", null, TodoPriority.Normal);
        var child = await fixture.Service.CreateChildTodoAsync(parent.Id, "Step", null, TodoPriority.Normal);
        await fixture.Service.ToggleCompletedAsync(child.Id, true);

        await fixture.Service.ToggleCompletedAsync(child.Id, false);

        var savedParent = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == parent.Id);
        Assert.False(savedParent.IsCompleted);
        Assert.Null(savedParent.CompletedAt);
    }

    [Fact]
    public async Task DeleteRootTodo_CascadesChildrenAndPinnedSettings()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var parent = await fixture.Service.CreateRootTodoAsync("Pinned project", null, TodoPriority.Normal);
        var child = await fixture.Service.CreateChildTodoAsync(parent.Id, "Nested task", null, TodoPriority.Normal);
        fixture.Context.PinnedWindowSettings.Add(new PinnedWindowSetting { TodoItemId = parent.Id });
        await fixture.Context.SaveChangesAsync();

        await fixture.Service.DeleteTodoAsync(parent.Id);

        Assert.False(await fixture.Context.TodoItems.AnyAsync(x => x.Id == parent.Id));
        Assert.False(await fixture.Context.TodoItems.AnyAsync(x => x.Id == child.Id));
        Assert.False(await fixture.Context.PinnedWindowSettings.AnyAsync(x => x.TodoItemId == parent.Id));
    }

    [Fact]
    public async Task CompletingRecurringRoot_DoesNotCreateNextInstanceByDefault()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var dueAt = new DateTime(2026, 6, 18, 18, 0, 0);
        var parent = await fixture.Service.CreateRootTodoAsync("Daily plan", dueAt, TodoPriority.High);
        await fixture.Service.CreateChildTodoAsync(parent.Id, "Review notes", null, TodoPriority.Normal);
        await fixture.Service.UpdateTodoAsync(parent.Id, parent.Title, string.Empty, dueAt, parent.Priority, RepeatType.Daily, 1, false, 0, 30, 3);

        await fixture.Service.ToggleCompletedAsync(parent.Id, true);

        Assert.False(await fixture.Context.TodoItems.AnyAsync(x => x.CreatedFromRecurringTaskId == parent.Id));
    }

    [Fact]
    public async Task CompletingRecurringRoot_CreatesNextInstanceWithChildrenWhenEnabled()
    {
        await using var fixture = await TestFixture.CreateAsync(autoCreateNextRecurringTodos: true);
        var dueAt = new DateTime(2026, 6, 18, 18, 0, 0);
        var parent = await fixture.Service.CreateRootTodoAsync("Daily plan", dueAt, TodoPriority.High);
        await fixture.Service.CreateChildTodoAsync(parent.Id, "Review notes", null, TodoPriority.Normal);
        await fixture.Service.UpdateTodoAsync(parent.Id, parent.Title, string.Empty, dueAt, parent.Priority, RepeatType.Daily, 1, false, 0, 30, 3);

        await fixture.Service.ToggleCompletedAsync(parent.Id, true);

        var next = await fixture.Context.TodoItems
            .AsNoTracking()
            .Include(x => x.Children)
            .SingleAsync(x => x.CreatedFromRecurringTaskId == parent.Id);
        Assert.Equal(dueAt.AddDays(1), next.DueAt);
        Assert.Equal("Daily plan", next.Title);
        Assert.Single(next.Children);
    }

    [Theory]
    [InlineData(RepeatType.Secondly, 30, "2026-06-18T18:00:30")]
    [InlineData(RepeatType.Minutely, 5, "2026-06-18T18:05:00")]
    [InlineData(RepeatType.Daily, 2, "2026-06-20T18:00:00")]
    [InlineData(RepeatType.Weekly, 2, "2026-07-02T18:00:00")]
    [InlineData(RepeatType.Monthly, 1, "2026-07-18T18:00:00")]
    [InlineData(RepeatType.Custom, 3, "2026-06-21T18:00:00")]
    public void RecurrenceService_ReturnsExpectedNextOccurrence(RepeatType repeatType, int interval, string expected)
    {
        var service = new RecurrenceService();
        var dueAt = new DateTime(2026, 6, 18, 18, 0, 0);

        var next = service.GetNextOccurrence(dueAt, repeatType, interval);

        Assert.Equal(DateTime.Parse(expected), next);
    }

    [Theory]
    [InlineData("2026-06-19T18:00:00", "2026-06-22T18:00:00")]
    [InlineData("2026-06-22T18:00:00", "2026-06-23T18:00:00")]
    public void RecurrenceService_WeekdaysSkipsWeekend(string dueAtText, string expectedText)
    {
        var service = new RecurrenceService();

        var next = service.GetNextOccurrence(DateTime.Parse(dueAtText), RepeatType.Weekdays, 1);

        Assert.Equal(DateTime.Parse(expectedText), next);
    }

    [Fact]
    public async Task ReminderService_CheckNowSendsDueReminderAndSchedulesNext()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var dueAt = DateTime.Now.AddMinutes(-1);
        var todo = await fixture.Service.CreateRootTodoAsync("Reminder target", dueAt, TodoPriority.High);
        await fixture.Service.UpdateTodoAsync(todo.Id, todo.Title, string.Empty, dueAt, todo.Priority, RepeatType.None, 1, true, 0, 15, 2);
        var notifications = new RecordingNotificationService();
        var reminderService = new ReminderService(
            fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            notifications,
            new CountdownService(),
            new TestLoggingService());

        await reminderService.CheckNowAsync();

        Assert.Single(notifications.Messages);
        var saved = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == todo.Id);
        Assert.Equal(1, saved.ReminderSentCount);
        Assert.NotNull(saved.LastReminderAt);
        Assert.NotNull(saved.NextReminderAt);
        Assert.True(saved.NextReminderAt > DateTime.Now);
    }

    [Fact]
    public async Task ReminderService_RecurringReminderAdvancesToNextOccurrence()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var dueAt = DateTime.Now.AddMinutes(-1);
        var todo = await fixture.Service.CreateRootTodoAsync("Daily reminder", dueAt, TodoPriority.High, RepeatType.Daily, 1);
        var notifications = new RecordingNotificationService();
        var reminderService = new ReminderService(
            fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            notifications,
            new CountdownService(),
            new TestLoggingService(),
            new RecurrenceService());

        await reminderService.CheckNowAsync();

        Assert.Single(notifications.Messages);
        var saved = await fixture.Context.TodoItems.AsNoTracking().SingleAsync(x => x.Id == todo.Id);
        Assert.Equal(dueAt.AddDays(1), saved.DueAt);
        Assert.Equal(saved.DueAt, saved.NextReminderAt);
        Assert.Equal(0, saved.ReminderSentCount);
    }

    [Fact]
    public async Task PinnedWindowService_SaveSettingAsync_PersistsWindowSettings()
    {
        await using var fixture = await TestFixture.CreateAsync();
        var todo = await fixture.Service.CreateRootTodoAsync("Pinned task", null, TodoPriority.Normal);
        var setting = new PinnedWindowSetting { TodoItemId = todo.Id, Left = 10, Top = 20, Width = 300, Height = 400 };
        fixture.Context.PinnedWindowSettings.Add(setting);
        await fixture.Context.SaveChangesAsync();
        var service = new PinnedWindowService(
            fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            new CountdownService(),
            new TestLoggingService());

        setting.Left = 111;
        setting.Top = 222;
        setting.Width = 333;
        setting.Height = 444;
        setting.Opacity = 0.75;
        setting.IsTopMost = false;
        setting.IsLocked = true;
        setting.ShowCompleted = false;
        await service.SaveSettingAsync(setting);

        var saved = await fixture.Context.PinnedWindowSettings.AsNoTracking().SingleAsync(x => x.Id == setting.Id);
        Assert.Equal(111, saved.Left);
        Assert.Equal(222, saved.Top);
        Assert.Equal(333, saved.Width);
        Assert.Equal(444, saved.Height);
        Assert.Equal(0.75, saved.Opacity);
        Assert.False(saved.IsTopMost);
        Assert.True(saved.IsLocked);
        Assert.False(saved.ShowCompleted);
    }

    [Fact]
    public async Task MainViewModel_LoadFiltersByTodayAndSearchText()
    {
        await using var fixture = await TestFixture.CreateAsync();
        await fixture.Service.CreateRootTodoAsync("Today focus", DateTime.Today.AddHours(18), TodoPriority.High);
        await fixture.Service.CreateRootTodoAsync("Future plan", DateTime.Today.AddDays(3).AddHours(18), TodoPriority.Normal);
        var viewModel = new MainViewModel(
            fixture.Service,
            new CountdownService(),
            new TestDialogService(),
            new TestLoggingService(),
            new TestPinnedWindowService());

        await viewModel.LoadAsync();
        viewModel.SelectedFilter = "Today";
        viewModel.SearchText = "focus";

        Assert.Single(viewModel.Todos);
        Assert.Equal("Today focus", viewModel.Todos[0].Title);
    }

    [Fact]
    public void CountdownService_ReturnsOverdueText()
    {
        var service = new CountdownService();
        var todo = new TodoItem { Title = "Overdue task", DueAt = new DateTime(2026, 6, 15, 8, 0, 0) };

        var text = service.GetCountdownText(todo, new DateTime(2026, 6, 16, 10, 0, 0));

        Assert.Contains("\u5df2\u903e\u671f", text);
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestFixture(SqliteConnection connection, FocusTodoDbContext context, ITodoService service, ServiceProvider serviceProvider)
        {
            _connection = connection;
            Context = context;
            Service = service;
            ServiceProvider = serviceProvider;
        }

        public FocusTodoDbContext Context { get; }
        public ITodoService Service { get; }
        public ServiceProvider ServiceProvider { get; }

        public static async Task<TestFixture> CreateAsync(bool autoCreateNextRecurringTodos = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<FocusTodoDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new FocusTodoDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var repository = new TodoRepository(context);
            var preferencesService = new TestPreferencesService(autoCreateNextRecurringTodos);
            var service = new TodoService(repository, new RecurrenceService(), new TestLoggingService(), preferencesService);

            var services = new ServiceCollection();
            services.AddDbContext<FocusTodoDbContext>(builder => builder.UseSqlite(connection));
            services.AddScoped<ITodoRepository, TodoRepository>();
            services.AddSingleton<IAppPreferencesService>(preferencesService);
            services.AddScoped<ITodoService>(provider => new TodoService(
                provider.GetRequiredService<ITodoRepository>(),
                new RecurrenceService(),
                new TestLoggingService(),
                provider.GetRequiredService<IAppPreferencesService>()));
            var serviceProvider = services.BuildServiceProvider();

            return new TestFixture(connection, context, service, serviceProvider);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await ServiceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RecordingNotificationService : INotificationService
    {
        public List<(string Title, string Message)> Messages { get; } = [];

        public Task ShowAsync(string title, string message, CancellationToken cancellationToken = default)
        {
            Messages.Add((title, message));
            return Task.CompletedTask;
        }
    }

    private sealed class TestDialogService : IDialogService
    {
        public bool Confirm(string title, string message) => true;
        public void ShowInfo(string title, string message) { }
        public void ShowError(string title, string message) { }
    }

    private sealed class TestLoggingService : ILoggingService
    {
        public void LogError(Exception exception, string message) { }
        public void LogInfo(string message) { }
    }

    private sealed class TestPinnedWindowService : IPinnedWindowService
    {
        public Task OpenPinnedWindowAsync(Guid todoItemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task OpenSavedWindowsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveSettingAsync(PinnedWindowSetting setting, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnpinAsync(Guid todoItemId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestPreferencesService : IAppPreferencesService
    {
        public TestPreferencesService(bool autoCreateNextRecurringTodos)
        {
            Preferences = new AppPreferences
            {
                DbDirectory = AppContext.BaseDirectory,
                Language = "zh-CN",
                AutoCreateNextRecurringTodos = autoCreateNextRecurringTodos
            };
        }

        public string ConfigDirectory => AppContext.BaseDirectory;
        public AppPreferences Preferences { get; }
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
