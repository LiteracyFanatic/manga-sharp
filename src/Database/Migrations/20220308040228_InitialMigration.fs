﻿// <auto-generated />
namespace manga.Migrations

open System
open MangaSharp.Database
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Infrastructure
open Microsoft.EntityFrameworkCore.Metadata
open Microsoft.EntityFrameworkCore.Migrations
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

[<DbContext(typeof<MangaContext>)>]
[<Migration("20220308040228_InitialMigration")>]
type InitialMigration() =
    inherit Migration()

    override this.Up(migrationBuilder: MigrationBuilder) =
        migrationBuilder.CreateTable(
            name = "Manga",
            columns =
                (fun table -> {|
                    Id = table.Column<Guid>(nullable = false, ``type`` = "TEXT")
                    Title = table.Column<string>(nullable = false, ``type`` = "TEXT", maxLength = Nullable(1000))
                    Bookmark = table.Column<string>(nullable = true, ``type`` = "TEXT", maxLength = Nullable(10))
                    Direction = table.Column<string>(nullable = false, ``type`` = "TEXT", maxLength = Nullable(10))
                    Url = table.Column<string>(nullable = false, ``type`` = "TEXT", maxLength = Nullable(200))
                    Accessed = table.Column<DateTime>(nullable = true, ``type`` = "TEXT")
                |}),
            constraints = (fun table -> table.PrimaryKey("PK_Manga", (fun x -> (x.Id) :> obj)) |> ignore)
        )
        |> ignore

        migrationBuilder.CreateTable(
            name = "Chapters",
            columns =
                (fun table -> {|
                    Id = table.Column<Guid>(nullable = false, ``type`` = "TEXT")
                    Url = table.Column<string>(nullable = false, ``type`` = "TEXT", maxLength = Nullable(200))
                    Title = table.Column<string>(nullable = true, ``type`` = "TEXT", maxLength = Nullable(200))
                    DownloadStatus =
                        table.Column<string>(nullable = false, ``type`` = "TEXT", maxLength = Nullable(10))
                    MangaId = table.Column<Guid>(nullable = false, ``type`` = "TEXT")
                |}),
            constraints =
                (fun table ->
                    table.PrimaryKey("PK_Chapters", (fun x -> (x.Id) :> obj)) |> ignore

                    table.ForeignKey(
                        name = "FK_Chapters_Manga_MangaId",
                        column = (fun x -> (x.MangaId) :> obj),
                        principalTable = "Manga",
                        principalColumn = "Id",
                        onDelete = ReferentialAction.Cascade
                    )
                    |> ignore

                )
        )
        |> ignore

        migrationBuilder.CreateIndex(name = "IX_Chapters_MangaId", table = "Chapters", column = "MangaId")
        |> ignore

        migrationBuilder.CreateIndex(name = "IX_Manga_Title", table = "Manga", column = "Title", unique = true)
        |> ignore


    override this.Down(migrationBuilder: MigrationBuilder) =
        migrationBuilder.DropTable(name = "Chapters") |> ignore

        migrationBuilder.DropTable(name = "Manga") |> ignore


    override this.BuildTargetModel(modelBuilder: ModelBuilder) =
        modelBuilder.HasAnnotation("ProductVersion", "6.0.2") |> ignore

        modelBuilder.Entity(
            "MangaSharp.Database.MangaDomain+Chapter",
            (fun b ->

                b
                    .Property<Guid>("Id")
                    .IsRequired(true)
                    .ValueGeneratedOnAdd()
                    .HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string>("DownloadStatus")
                    .IsRequired(true)
                    .HasMaxLength(10)
                    .HasColumnType("TEXT")
                |> ignore

                b.Property<Guid>("MangaId").IsRequired(true).HasColumnType("TEXT") |> ignore

                b
                    .Property<string option>("Title")
                    .IsRequired(false)
                    .HasMaxLength(200)
                    .HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string>("Url")
                    .IsRequired(true)
                    .HasMaxLength(200)
                    .HasColumnType("TEXT")
                |> ignore

                b.HasKey("Id") |> ignore


                b.HasIndex("MangaId") |> ignore

                b.ToTable("Chapters") |> ignore

            )
        )
        |> ignore

        modelBuilder.Entity(
            "MangaSharp.Database.MangaDomain+Manga",
            (fun b ->

                b
                    .Property<Guid>("Id")
                    .IsRequired(true)
                    .ValueGeneratedOnAdd()
                    .HasColumnType("TEXT")
                |> ignore

                b.Property<DateTime option>("Accessed").IsRequired(false).HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string option>("Bookmark")
                    .IsRequired(false)
                    .HasMaxLength(10)
                    .HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string>("Direction")
                    .IsRequired(true)
                    .HasMaxLength(10)
                    .HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string>("Title")
                    .IsRequired(true)
                    .HasMaxLength(1000)
                    .HasColumnType("TEXT")
                |> ignore

                b
                    .Property<string>("Url")
                    .IsRequired(true)
                    .HasMaxLength(200)
                    .HasColumnType("TEXT")
                |> ignore

                b.HasKey("Id") |> ignore


                b.HasIndex("Title").IsUnique() |> ignore

                b.ToTable("Manga") |> ignore

            )
        )
        |> ignore

        modelBuilder.Entity(
            "MangaSharp.Database.MangaDomain+Chapter",
            (fun b ->
                b
                    .HasOne("MangaSharp.Database.MangaDomain+Manga", "Manga")
                    .WithMany("Chapters")
                    .HasForeignKey("MangaId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired()
                |> ignore

            )
        )
        |> ignore

        modelBuilder.Entity(
            "MangaSharp.Database.MangaDomain+Manga",
            (fun b ->

                b.Navigation("Chapters") |> ignore)
        )
        |> ignore
