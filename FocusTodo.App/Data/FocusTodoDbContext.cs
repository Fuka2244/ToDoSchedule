using FocusTodo.App.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusTodo.App.Data;

public sealed class FocusTodoDbContext(DbContextOptions<FocusTodoDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<PinnedWindowSetting> PinnedWindowSettings => Set<PinnedWindowSetting>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Description).HasDefaultValue(string.Empty);
            entity.Property(x => x.Priority).HasConversion<string>();
            entity.Property(x => x.RepeatType).HasConversion<string>();
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ParentId);
            entity.HasIndex(x => x.DueAt);
            entity.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<PinnedWindowSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.TodoItem)
                .WithMany()
                .HasForeignKey(x => x.TodoItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasData(new AppSetting());
        });
    }
}
