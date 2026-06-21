# FocusTodo

FocusTodo 是一个 Windows 桌面待办应用，使用 C#、WPF、MVVM、EF Core 和 SQLite 构建。它适合把一个大任务拆成多个小任务，并通过 DDL、倒计时、提醒、重复任务和桌面置顶窗口来辅助专注执行。

## 运行环境

- 操作系统：Windows
- .NET SDK：9.x
- 目标框架：`net9.0-windows`
- 运行架构：`win-x64`

当前项目依赖：

- `CommunityToolkit.Mvvm`
- `Microsoft.EntityFrameworkCore.Sqlite`
- `Microsoft.Extensions.DependencyInjection`
- 测试依赖：`xunit`、`Microsoft.NET.Test.Sdk`、`coverlet.collector`

## 启动方式

在项目根目录执行：

```powershell
$env:DOTNET_CLI_HOME='F:\vibecoding\ToDoSchedule\.dotnet-home'
dotnet restore FocusTodo.sln
dotnet run --project FocusTodo.App\FocusTodo.App.csproj
```

如果依赖已经还原过，也可以直接运行：

```powershell
dotnet run --project FocusTodo.App\FocusTodo.App.csproj
```

## 测试

```powershell
$env:DOTNET_CLI_HOME='F:\vibecoding\ToDoSchedule\.dotnet-home'
dotnet test FocusTodo.sln
```

当前测试覆盖了任务创建、父子任务完成联动、级联删除、重复任务、提醒扫描、置顶窗口设置保存和筛选搜索等核心行为。

## 发布

发布 win-x64 自包含版本：

```powershell
$env:DOTNET_CLI_HOME='F:\vibecoding\ToDoSchedule\.dotnet-home'
dotnet publish FocusTodo.App\FocusTodo.App.csproj -c Release -r win-x64 --self-contained true
```

发布产物会生成在：

```text
FocusTodo.App\bin\Release\net9.0-windows\win-x64\publish\
```

## 主要功能

- 两级任务结构：支持 Big Todo 和 Small Todo。
- 任务管理：新增、编辑、删除、完成和恢复任务。
- 父子联动：所有 Small Todo 完成后，Big Todo 自动完成；恢复子任务后，父任务也会恢复为未完成。
- DDL 管理：支持设置截止日期和时间。
- 动态倒计时：显示剩余时间、今日、逾期和已完成状态。
- 任务筛选：支持 All、Today、Future、Overdue、Completed。
- 搜索：按任务标题搜索 Big Todo 和 Small Todo。
- 优先级：支持 Low、Normal、High、Urgent。
- 重复任务：支持无重复、每 N 秒、每 N 分钟、每日、每周、每月、工作日和自定义间隔。
- 自动生成下一次任务：可在 Settings 中开启。开启后，重复 Big Todo 完成时会基于原 DDL 生成下一次任务，并复制子任务。
- 提醒：支持启用提醒、提前提醒、提醒间隔、最大提醒次数。
- 稍后提醒：支持 5、10、30、60 分钟后再次提醒。
- 系统托盘：关闭主窗口时隐藏到托盘，可从托盘重新打开或退出应用。
- 快速添加：托盘菜单支持快速添加一个 Todo。
- 桌面置顶窗口：可以把 Big Todo 固定成独立浮窗。
- 多个置顶窗口：支持同时打开多个置顶窗口。
- 置顶窗口设置：支持置顶、锁定位置、显示/隐藏已完成子任务、调整透明度。
- 置顶窗口持久化：窗口位置、大小、透明度等设置会保存到 SQLite。
- 多屏保护：启动时会把屏幕外的置顶窗口拉回可见区域。
- 设置：支持修改数据库存储目录、切换中文/英文、控制循环任务是否自动生成下一次。

## 如何使用

### 添加 Big Todo

1. 在顶部输入框填写任务标题。
2. 选择优先级。
3. 可选：设置 DDL 日期、小时和分钟。
4. 点击 `Add Big Todo`。

### 添加 Small Todo

1. 先点击某个任务右侧的 `Edit`，选中一个 Big Todo 或它下面的 Small Todo。
2. 在顶部输入框填写小任务标题。
3. 点击 `Add Small Todo`。
4. 如果当前选中的是 Small Todo，新 Small Todo 会添加到它所属的 Big Todo 下。

### 编辑任务

1. 点击任务右侧的 `Edit`。
2. 在右侧详情面板修改标题、描述、DDL、优先级、重复规则或提醒设置。
3. 点击 `Save` 保存。
4. 点击详情面板右上角的 `x` 可关闭详情面板。

### 完成和恢复任务

- 勾选任务左侧复选框即可完成任务。
- 取消勾选即可恢复任务。
- 完成 Big Todo 时，它下面的 Small Todo 会一起完成。
- 完成全部 Small Todo 时，Big Todo 会自动完成。

### 筛选和搜索

- 左侧菜单可切换：
  - `All`：全部任务
  - `Today`：今天到期
  - `Future`：未来到期
  - `Overdue`：已逾期
  - `Completed`：已完成
- 中间顶部搜索框可按任务标题搜索。

### 设置重复任务

1. 选中一个 Big Todo。
2. 在右侧详情面板选择 `Repeat` 类型。
3. 设置间隔数值。
4. 点击 `Save`。
5. 如果在 Settings 中开启了 `完成循环任务后自动生成下一次`，该 Big Todo 完成后会自动生成下一次任务。

注意：自动生成默认关闭，避免勾选完成时突然新增下一条任务。开启后，重复任务只对 Big Todo 自动生成下一次实例；Small Todo 会作为子任务模板被复制到下一次 Big Todo 中。

### 使用 Settings

点击左侧底部的 `Settings` 可打开设置面板。

当前支持：

- 修改数据库存储目录：保存后重启应用生效。新目录会使用 `focusTodo.db` 作为数据库文件名。
- 切换语言：支持 `zh-CN` 和 `en-US`，保存后界面文本会立即刷新。
- 控制循环任务：可选择完成循环任务后是否自动生成下一次。

### 设置提醒

1. 选中任务。
2. 勾选 `Enable reminder`。
3. 设置：
   - `Lead`：提前多少分钟提醒。
   - `Interval`：重复提醒间隔。
   - `Count`：最大提醒次数。
4. 点击 `Save`。

应用运行时会在后台扫描到期提醒，并通过系统托盘通知弹出提醒。

### 稍后提醒

选中任务后，在右侧详情面板点击：

- `5m`
- `10m`
- `30m`
- `1h`

即可把下一次提醒延后对应时间。

### 使用托盘菜单

主窗口关闭时不会直接退出，而是隐藏到系统托盘。

托盘菜单包含：

- `Open FocusTodo`：重新打开主窗口。
- `Quick Add Todo`：快速新增一个普通 Todo。
- `Test notification`：测试通知是否可用。
- `Pause reminders 1 hour`：暂停提醒 1 小时。
- `Resume reminders`：恢复提醒。
- `Exit`：完全退出应用。

### 使用置顶窗口

1. 选中一个 Big Todo 或它的任意 Small Todo。
2. 点击右侧详情面板的 `Pin Window`。
3. 系统会打开一个独立置顶浮窗。

置顶窗口中可以：

- 查看 Big Todo 标题、DDL 和倒计时。
- 勾选 Small Todo 完成状态。
- 直接新增 Small Todo。
- 设置是否置顶。
- 锁定窗口位置。
- 显示或隐藏已完成 Small Todo。
- 调整透明度。
- 右键选择 `Unpin` 取消固定。

## 数据和日志位置

SQLite 数据库：

```text
%LocalAppData%\FocusTodo\focusTodo.db
```

如果在 Settings 中修改了数据库存储目录，重启后数据库会位于：

```text
<设置的目录>\focusTodo.db
```

应用偏好配置文件位于：

```text
%LocalAppData%\FocusTodo\preferences.json
```

日志目录：

```text
.\Logs\
```

日志目录相对于应用启动目录生成。启动失败时会写入：

```text
.\Logs\crash-yyyyMMdd.log
```

## 项目结构

```text
FocusTodo.sln
Directory.Build.props
README.md
FocusTodo.App/
  App.xaml
  MainWindow.xaml
  PinnedTodoWindow.xaml
  Data/
  Models/
  Repositories/
  Services/
  ViewModels/
FocusTodo.Tests/
  TodoServiceTests.cs
```

## 数据库初始化

应用启动时会通过 `Database.EnsureCreatedAsync()` 自动创建 SQLite 数据库和表结构。

这适合当前 MVP 阶段。后续如果模型频繁变化，建议改为 EF Core Migrations，并在启动时使用 `Database.MigrateAsync()`。

## 常见问题

### 关闭窗口后应用还在运行

这是预期行为。关闭主窗口会隐藏到托盘。如果要完全退出，请使用托盘菜单里的 `Exit`。

### 收不到提醒

请确认：

- 应用正在运行。
- 任务已启用 `Enable reminder`。
- 任务未完成。
- `Count` 没有达到最大提醒次数。
- Windows 没有禁用托盘/气泡通知。

### NuGet 还原失败

如果 `dotnet restore` 或 `dotnet test` 无法访问 NuGet，请检查网络、代理或 NuGet 源配置。
