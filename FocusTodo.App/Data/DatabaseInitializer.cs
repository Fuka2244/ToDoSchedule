namespace FocusTodo.App.Data;

public sealed class DatabaseInitializer(FocusTodoDbContext dbContext) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
