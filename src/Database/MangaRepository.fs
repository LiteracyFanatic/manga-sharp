namespace MangaSharp.Database

open System
open System.Collections.Generic
open Microsoft.EntityFrameworkCore
open EntityFrameworkCore.FSharp.DbContextHelpers

open MangaSharp.Database.MangaDomain

type MangaRepository(db: MangaContext) =

    member this.GetByIdAsync(mangaId: Guid) =
        db.Manga
            .Include(fun manga -> manga.Chapters :> IEnumerable<_>)
            .ThenInclude(fun (chapter: Chapter) -> chapter.Pages)
            .AsSplitQuery()
            .FirstAsync(fun m -> m.Id = mangaId)

    member this.GetOrCreateAsync(title: string, direction: Direction, url: string) =
        task {
            let! existingManga =
                db.Manga
                    .Include(fun manga -> manga.Chapters :> IEnumerable<_>)
                    .ThenInclude(fun (chapter: Chapter) -> chapter.Pages)
                    .AsSplitQuery()
                    .TryFirstAsync(fun m -> m.Title = title)

            let manga =
                existingManga
                |> Option.defaultWith (fun () ->
                    db.Manga.Add(Manga(Title = title, Direction = direction, Url = url)).Entity)

            return manga
        }
