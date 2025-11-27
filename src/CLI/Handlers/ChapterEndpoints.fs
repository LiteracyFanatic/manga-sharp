namespace MangaSharp.CLI.Server

open System
open System.Collections.Generic
open System.Linq
open Microsoft.EntityFrameworkCore
open Giraffe
open Giraffe.EndpointRouting
open MangaSharp.Database
open MangaSharp.CLI.Util

module ChapterEndpoints =

    [<CLIMutable>]
    type ChapterGetResponse = {
        MangaId: Guid
        MangaTitle: string
        Direction: Direction
        ChapterId: Guid
        ChapterTitle: string option
        PreviousChapterUrl: string option
        NextChapterUrl: string option
        OtherChapters:
            {| Id: Guid
               Title: string
               Url: string
               DownloadStatus: DownloadStatus |}[]
        DownloadStatus: DownloadStatus
        Pages:
            {| Id: Guid
               Name: string
               Width: int
               Height: int |}[]
    }

    let private getChapterHandler (chapterId: Guid) : HttpHandler =
        fun next ctx ->
            task {
                let db = ctx.GetService<MangaContext>()

                let! manga =
                    db.Manga
                        .Include(fun manga ->
                            manga.Chapters
                                .Where(fun chapter -> chapter.Title <> null)
                                .OrderBy(fun chapter -> chapter.Index)
                            :> IEnumerable<_>)
                        .ThenInclude(fun (chapter: Chapter) -> chapter.Pages.OrderBy(fun page -> page.Name))
                        .AsSplitQuery()
                        .FirstAsync(fun manga -> manga.Chapters.Select(fun c -> c.Id).Contains(chapterId))

                manga.Accessed <- DateTime.UtcNow
                let! _ = db.SaveChangesAsync()
                let chapter = manga.Chapters |> Seq.find (fun c -> c.Id = chapterId)

                let getQueryParams (page: Page) =
                    match manga.Direction with
                    | Direction.Horizontal -> $"?page=%s{page.Name}"
                    | Direction.Vertical -> ""
                    | _ -> ArgumentOutOfRangeException() |> raise

                let response = {
                    MangaId = chapter.MangaId
                    MangaTitle = manga.Title
                    Direction = chapter.Manga.Direction
                    ChapterId = chapter.Id
                    ChapterTitle = Option.ofObj chapter.Title
                    PreviousChapterUrl =
                        tryPreviousChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryLast
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title}%s{queryParams}")
                    NextChapterUrl =
                        tryNextChapter manga chapter
                        |> Option.map (fun c ->
                            let queryParams =
                                c.Pages
                                |> Seq.sortBy (fun p -> p.Name)
                                |> Seq.tryHead
                                |> Option.map getQueryParams
                                |> Option.defaultValue ""

                            $"/chapters/%A{c.Id}/%s{slugify manga.Title}/%s{c.Title}%s{queryParams}")
                    OtherChapters =
                        manga.Chapters
                        |> Seq.map (fun chapter -> {|
                            Id = chapter.Id
                            Title = chapter.Title
                            Url = $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title}"
                            DownloadStatus = chapter.DownloadStatus
                        |})
                        |> Seq.toArray
                    DownloadStatus = chapter.DownloadStatus
                    Pages =
                        chapter.Pages
                        |> Seq.distinctBy (fun page -> page.Name)
                        |> Seq.sortBy (fun page -> page.Name)
                        |> Seq.map (fun page -> {|
                            Id = page.Id
                            Name = page.Name
                            Width = page.Width
                            Height = page.Height
                        |})
                        |> Seq.toArray
                }

                return! json response next ctx
            }

    let routes : Endpoint list =
        [
            GET [ routef "/chapters/%O" getChapterHandler ]
        ]
