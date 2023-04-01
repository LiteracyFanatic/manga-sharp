namespace MangaSharp.Database

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design
open EntityFrameworkCore.FSharp.Extensions

module rec MangaDomain =
    let makeUnion (unionType: Type) (v: string) =
        let cases = Reflection.FSharpType.GetUnionCases(unionType)
        let case = cases |> Array.find (fun case -> case.Name = v)
        Reflection.FSharpValue.MakeUnion(case, [||])

    type Direction =
        | Horizontal
        | Vertical

    type DownloadStatus =
        | NotDownloaded
        | Downloaded
        | Archived
        | Ignored

    type Manga() =
        member val Id: Guid = Unchecked.defaultof<_> with get, set
        member val Title: string = Unchecked.defaultof<_> with get, set
        member val Chapters: List<Chapter> = List<_>() with get, set
        member val Direction: Direction = Unchecked.defaultof<_> with get, set
        member val Url: string = Unchecked.defaultof<_> with get, set
        member val Created: DateTime = Unchecked.defaultof<_> with get, set
        member val Accessed: DateTime option = Unchecked.defaultof<_> with get, set

        [<DefaultValue>]
        val mutable private _BookmarkChapterId: Nullable<Guid>

        member this.BookmarkChapterId
            with get () = Option.ofNullable this._BookmarkChapterId
            and set v = this._BookmarkChapterId <- Option.toNullable v

        [<DefaultValue>]
        val mutable private _BookmarkChapter: Chapter

        member this.BookmarkChapter
            with get () = Option.ofObj this._BookmarkChapter
            and set v = this._BookmarkChapter <- Option.toObj v

        [<DefaultValue>]
        val mutable private _BookmarkPageId: Nullable<Guid>

        member this.BookmarkPageId
            with get () = Option.ofNullable this._BookmarkPageId
            and set v = this._BookmarkPageId <- Option.toNullable v

        [<DefaultValue>]
        val mutable private _BookmarkPage: Page

        member this.BookmarkPage
            with get () = Option.ofObj this._BookmarkPage
            and set v = this._BookmarkPage <- Option.toObj v

        interface IEntityTypeConfiguration<Manga> with
            member this.Configure(builder) =
                builder.Property(fun e -> e.Title).HasMaxLength(1000) |> ignore

                builder.HasIndex(fun e -> e.Title :> obj).IsUnique() |> ignore

                builder
                    .Ignore(fun e -> e.BookmarkChapterId :> obj)
                    .Property(fun e -> e._BookmarkChapterId)
                    .HasColumnName("BookmarkChapterId")
                |> ignore

                builder
                    .Ignore(fun e -> e.BookmarkChapter :> obj)
                    .HasOne(fun e -> e._BookmarkChapter)
                    .WithOne()
                    .HasForeignKey(fun (e: Manga) -> e._BookmarkChapterId :> obj)
                |> ignore

                builder
                    .Ignore(fun e -> e.BookmarkPageId :> obj)
                    .Property(fun e -> e._BookmarkPageId)
                    .HasColumnName("BookmarkPageId")
                |> ignore

                builder
                    .Ignore(fun e -> e.BookmarkPage :> obj)
                    .HasOne(fun e -> e._BookmarkPage)
                    .WithOne()
                    .HasForeignKey(fun (e: Manga) -> e._BookmarkPageId :> obj)
                |> ignore

                builder
                    .Property(fun e -> e.Direction)
                    .HasConversion((fun v -> v.ToString()), (fun v -> makeUnion (typeof<Direction>) v :?> Direction))
                    .HasMaxLength(10)
                |> ignore

                builder.Property(fun e -> e.Url).HasMaxLength(200) |> ignore

                builder.Property(fun e -> e.Created).HasDefaultValueSql("datetime()") |> ignore

    [<AllowNullLiteral>]
    type Chapter() =
        member val Id: Guid = Unchecked.defaultof<_> with get, set
        member val Url: string = Unchecked.defaultof<_> with get, set
        member val Title: string option = Unchecked.defaultof<_> with get, set
        member val Index: int = Unchecked.defaultof<_> with get, set
        member val DownloadStatus: DownloadStatus = Unchecked.defaultof<_> with get, set
        member val Created: DateTime = Unchecked.defaultof<_> with get, set
        member val MangaId: Guid = Unchecked.defaultof<_> with get, set
        member val Manga: Manga = Unchecked.defaultof<_> with get, set
        member val Pages: List<Page> = List<Page>() with get, set

        interface IEntityTypeConfiguration<Chapter> with
            member this.Configure(builder) =
                builder.Property(fun e -> e.Url).HasMaxLength(200) |> ignore

                builder.Property(fun e -> e.Title).HasMaxLength(200) |> ignore

                builder
                    .Property(fun e -> e.DownloadStatus)
                    .HasConversion(
                        (fun v -> v.ToString()),
                        (fun v -> makeUnion (typeof<DownloadStatus>) v :?> DownloadStatus)
                    )
                    .HasMaxLength(10)
                |> ignore

                builder.Property(fun e -> e.Created).HasDefaultValueSql("datetime()") |> ignore

    [<AllowNullLiteral>]
    type Page() =
        member val Id: Guid = Unchecked.defaultof<_> with get, set
        member val Name: string = Unchecked.defaultof<_> with get, set
        member val File: string = Unchecked.defaultof<_> with get, set
        member val Width: int = Unchecked.defaultof<_> with get, set
        member val Height: int = Unchecked.defaultof<_> with get, set
        member val ChapterId: Guid = Unchecked.defaultof<_> with get, set
        member val Chapter: Chapter = Unchecked.defaultof<_> with get, set

        interface IEntityTypeConfiguration<Page> with
            member this.Configure(builder) =
                builder.Property(fun e -> e.Name).HasMaxLength(10) |> ignore

                builder.Property(fun e -> e.File).HasMaxLength(2000) |> ignore

open MangaDomain

type MangaContext =
    inherit DbContext

    new() = { inherit DbContext() }
    new(options: DbContextOptions<MangaContext>) = { inherit DbContext(options) }

    [<DefaultValue>]
    val mutable private _Manga: DbSet<Manga>

    [<DefaultValue>]
    val mutable private _Chapters: DbSet<Chapter>

    [<DefaultValue>]
    val mutable private _Pages: DbSet<Page>

    member this.Manga
        with get () = this._Manga
        and set v = this._Manga <- v

    member this.Chapters
        with get () = this._Chapters
        and set v = this._Chapters <- v

    member this.Pages
        with get () = this._Pages
        and set v = this._Pages <- v

    override this.OnModelCreating(modelBuilder: ModelBuilder) =
        base.OnModelCreating(modelBuilder)

        modelBuilder.RegisterOptionTypes()

        for entity in modelBuilder.Model.GetEntityTypes() do
            for property in entity.ClrType.GetProperties() do
                if property.GetType() = typeof<string> then
                    modelBuilder
                        .Entity(property.DeclaringType)
                        .Property(property.PropertyType, property.Name)
                        .IsRequired()
                    |> ignore

        modelBuilder.ApplyConfigurationsFromAssembly(typeof<MangaContext>.Assembly)
        |> ignore

type MangaContextFactory() =
    interface IDesignTimeDbContextFactory<MangaContext> with
        member this.CreateDbContext(args: string[]) =
            let optionsBuilder = DbContextOptionsBuilder<MangaContext>()
            optionsBuilder.UseSqlite() |> ignore
            new MangaContext(optionsBuilder.Options)
