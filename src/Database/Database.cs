﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MangaSharp.Database;

public enum Direction
{
    Horizontal,
    Vertical
}

public enum DownloadStatus
{
    NotDownloaded,
    Downloaded,
    Archived,
    Ignored
}

public class Manga : IEntityTypeConfiguration<Manga>
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public List<Chapter> Chapters { get; set; } = [];
    public required Direction Direction { get; set; }
    public required string Url { get; set; }
    public DateTime Created { get; set; }
    public DateTime? Accessed { get; set; }
    public Guid? BookmarkChapterId { get; set; }
    public Chapter? BookmarkChapter { get; set; }
    public Guid? BookmarkPageId { get; set; }
    public Page? BookmarkPage { get; set; }

    public void Configure(EntityTypeBuilder<Manga> builder)
    {
        builder.Property(e => e.Title).HasMaxLength(1000);

        builder.HasIndex(e => e.Title).IsUnique();

        builder.HasOne(e => e.BookmarkChapter).WithOne().HasForeignKey((Manga e) => e.BookmarkChapterId);

        builder
            .Ignore(e => e.BookmarkPage)
            .HasOne(e => e.BookmarkPage)
            .WithOne()
            .HasForeignKey((Manga e) => e.BookmarkPageId);

        builder.Property(e => e.Direction).HasConversion(new EnumToStringConverter<Direction>()).HasMaxLength(10);

        builder.Property(e => e.Url).HasMaxLength(200);

        builder.Property(e => e.Created).HasDefaultValueSql("datetime()");
    }
}

public class Chapter : IEntityTypeConfiguration<Chapter>
{
    public Guid Id { get; set; }
    public required string Url { get; set; }
    public required string? Title { get; set; }
    public int Index { get; set; }
    public required DownloadStatus DownloadStatus { get; set; }
    public DateTime Created { get; set; }
    public Guid MangaId { get; set; }
    public Manga Manga { get; set; }
    public List<Page> Pages { get; set; } = [];

    public void Configure(EntityTypeBuilder<Chapter> builder)
    {
        builder.Property(e => e.Url).HasMaxLength(200);

        builder.Property(e => e.Title).HasMaxLength(200);

        builder
            .Property(e => e.DownloadStatus)
            .HasConversion(new EnumToStringConverter<DownloadStatus>())
            .HasMaxLength(10);

        builder.Property(e => e.Created).HasDefaultValueSql("datetime()");
    }
}

public class Page : IEntityTypeConfiguration<Page>
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string File { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public Guid ChapterId { get; set; }
    public Chapter Chapter { get; set; }

    public void Configure(EntityTypeBuilder<Page> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(10);

        builder.Property(e => e.File).HasMaxLength(2000);
    }
}

public class MangaContext : DbContext
{
    public MangaContext()
    {
    }

    public MangaContext(DbContextOptions<MangaContext> options) : base(options)
    {
    }

    public DbSet<Manga> Manga { get; set; } = null!;
    public DbSet<Chapter> Chapters { get; set; } = null!;
    public DbSet<Page> Pages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MangaContext).Assembly);
    }
}

public class MangaContextFactory : IDesignTimeDbContextFactory<MangaContext>
{
    public MangaContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MangaContext>();
        optionsBuilder.UseSqlite();
        return new MangaContext(optionsBuilder.Options);
    }
}
