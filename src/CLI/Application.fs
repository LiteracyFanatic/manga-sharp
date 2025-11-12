namespace MangaSharp.CLI

open System.IO
open System.Collections.Generic
open System.Linq
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open EntityFrameworkCore.FSharp.DbContextHelpers
open Argu
open MangaSharp
open MangaSharp.Database
open MangaSharp.Extractors
open MangaSharp.Extractors.Util
open MangaSharp.CLI.Arguments
open MangaSharp.CLI.Util
open Giraffe
open FsToolkit.ErrorHandling

type LsJson = {
    Title: string
    Direction: Direction
    NumberOfChapters: int
    BookmarkChapter: string option
    BookmarkPage: string option
    BookmarkUrl: string option
    FirstPageUrl: string option
    Url: string
}

type Application
    (
        versionInfo: VersionInfo,
        db: MangaContext,
        extractors: IEnumerable<IMangaExtractor>,
        jsonSerializer: Json.ISerializer,
        logger: ILogger<Application>,
        mangaService: MangaService
    ) =

    let ensureExtractors (urls: string list) =
        let foundExtractors, errors =
            List.foldBack
                (fun url (foundExtractors, errors) ->
                    match extractors |> Seq.tryFind (fun extractor -> extractor.IsMatch(url)) with
                    | Some m -> m :: foundExtractors, errors
                    | None -> foundExtractors, url :: errors)
                urls
                ([], [])

        match errors with
        | [] -> Ok foundExtractors
        | _ -> Error errors

    let makeDirectionUrlPairs (args: DownloadArgs list) =
        let rec loop direction acc args =
            match args with
            | [] -> acc
            | Direction d :: t -> loop d acc t
            | Url u :: t -> loop direction ((direction, u) :: acc) t

        loop Direction.Horizontal [] args |> List.rev

    let lsPlain () =
        let manga =
            db.Manga
                .AsNoTracking()
                .Include(fun m -> m.BookmarkChapter)
                .Include(fun m -> m.BookmarkPage)
                .Include(fun m -> m.Chapters)
                .OrderBy(fun m -> m.Title)
                .ToList()
            |> List.ofSeq

        manga
        |> List.iter (fun m ->
            let bookmarkText =
                m.BookmarkChapter
                |> Option.ofObj
                |> Option.map (fun c ->
                    let pageText =
                        m.BookmarkPage
                        |> Option.ofObj
                        |> Option.map (fun p -> sprintf " Page %s" p.Name)
                        |> Option.defaultValue ""

                    $"Chapter %s{c.Title}%s{pageText}")
                |> Option.defaultValue "None"

            let chapterCount =
                m.Chapters.Where(fun c -> c.DownloadStatus = DownloadStatus.Downloaded).Count()

            printfn $"%s{m.Title},%A{m.Direction},%i{chapterCount},%s{bookmarkText}")

    let lsJson () =
        let manga =
            db.Manga
                .AsNoTracking()
                .Include(fun m -> m.BookmarkChapter)
                .Include(fun m -> m.BookmarkPage)
                .Include(fun m -> m.Chapters)
                .OrderBy(fun m -> m.Title)
                .ToList()
            |> List.ofSeq
            |> List.map (fun m ->
                let chapters =
                    m.Chapters
                        .Where(fun c ->
                            c.DownloadStatus = DownloadStatus.Downloaded
                            || c.DownloadStatus = DownloadStatus.Archived)
                        .OrderBy(fun c -> c.Index)
                        .ToList()

                let firstPageUrl =
                    chapters
                    |> Seq.tryHead
                    |> Option.map (fun c -> $"/chapters/%A{c.Id}/%s{slugify m.Title}/%s{c.Title}")

                {
                    Title = m.Title
                    Direction = m.Direction
                    NumberOfChapters = Seq.length chapters
                    BookmarkChapter = m.BookmarkChapter |> Option.ofObj |> Option.map (fun c -> c.Title)
                    BookmarkPage = m.BookmarkPage |> Option.ofObj |> Option.map (fun p -> p.Name)
                    BookmarkUrl = Some(getBookmarkUrl m)
                    FirstPageUrl = firstPageUrl
                    Url = m.Url
                })

        let json = jsonSerializer.SerializeToString(manga)
        printfn $"%s{json}"

    member this.Download(args: ParseResults<DownloadArgs>) =
        if not (args.Contains(Url)) then
            args.Raise("Argument --url is required")

        let directions, urls = args.GetAllResults() |> makeDirectionUrlPairs |> List.unzip

        match ensureExtractors urls with
        | Ok extractors ->
            List.zip3 urls directions extractors
            |> List.iter (fun (u, d, e) ->
                let res = e.DownloadAsync(u, d) |> Async.AwaitTask |> Async.RunSynchronously

                match res with
                | Ok _ -> ()
                | Error e -> logger.LogError("SOMETHING WENT WRONG: {Error}", e))
        | Error u ->
            logger.LogError("Could not find a provider for the following URLs: {Urls}", u)
            exit 1

    member this.Update(args: ParseResults<UpdateArgs>) =
        let allManga =
            db.Manga.AsNoTracking().Include(fun m -> m.Chapters).OrderBy(fun m -> m.Title).ToList()
            |> List.ofSeq

        let manga =
            match
                args.TryGetResult(UpdateArgs.Title),
                args.TryGetResult(UpdateArgs.From),
                args.TryGetResult(UpdateArgs.To)
            with
            | None, None, None -> allManga
            | None, startTitle, endTitle ->
                let startIndex =
                    startTitle
                    |> Option.map (fun startTitle ->
                        allManga
                        |> List.tryFindIndex (fun m -> m.Title = startTitle)
                        |> Option.defaultWith (fun () ->
                            logger.LogError("Manga {Title} could not be found", startTitle)
                            exit 1))
                    |> Option.defaultValue 0

                let endIndex =
                    endTitle
                    |> Option.map (fun endTitle ->
                        allManga
                        |> List.tryFindIndex (fun m -> m.Title = endTitle)
                        |> Option.defaultWith (fun () ->
                            logger.LogError("Manga {Title} could not be found", endTitle)
                            exit 1))
                    |> Option.defaultValue (allManga.Length - 1)

                allManga[startIndex..endIndex]
            | Some title, startTitle, endTitle ->
                if startTitle.IsSome || endTitle.IsSome then
                    args.Raise("Cannot specify --title and --from/--to at the same")

                match allManga |> List.tryFind (fun m -> m.Title = title) with
                | Some m -> [ m ]
                | None ->
                    logger.LogError("Manga {Title} could not be found", title)
                    exit 1

        let updated =
            manga
            |> Seq.toList
            |> List.map (fun m ->
                logger.LogInformation("Checking {Title} for updates", m.Title)

                match extractors |> Seq.tryFind (fun extractor -> extractor.IsMatch(m.Url)) with
                | Some extractor ->
                    let res = extractor.UpdateAsync(m.Id) |> Async.AwaitTask |> Async.RunSynchronously

                    match res with
                    | Ok _ -> ()
                    | Error e -> logger.LogError("SOMETHING WENT WRONG: {Error}", e)

                    res
                | None ->
                    logger.LogError("Could not find a provider for the following URL: {Url}", m.Url)
                    Ok false)
            |> List.contains (Ok(true))

        if not updated then
            logger.LogInformation("No updates were found")

    member this.Ls(args: ParseResults<LsArgs>) =
        if args.Contains(Json) then lsJson () else lsPlain ()

    member this.Rm(args: ParseResults<RmArgs>) =
        match args.Contains(RmArgs.All), args.TryGetResult(RmArgs.Title) with
        | true, None ->
            db.Manga
                .OrderBy(fun m -> m.Title)
                .Select(fun m -> m.Id)
                .ToList()
                .ForEach(fun id -> mangaService.Delete(id) |> Async.AwaitTask |> Async.RunSynchronously)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            match db.Manga.TryFirst(fun m -> m.Title = title) with
            | Some m -> mangaService.Delete(m.Id) |> Async.AwaitTask |> Async.RunSynchronously
            | None ->
                logger.LogError("Manga {Title} could not be found", title)
                exit 1

    member this.Archive(args: ParseResults<ArchiveArgs>) =
        match args.Contains(ArchiveArgs.All), args.TryGetResult(ArchiveArgs.Title) with
        | true, None ->
            if args.Contains(ArchiveArgs.From_Chapter) || args.Contains(ArchiveArgs.To_Chapter) then
                args.Raise("Cannot specify --all in combination with --from-chapter or --to-chapter")
            else
                db.Manga
                    .OrderBy(fun m -> m.Title)
                    .Select(fun m -> m.Id)
                    .ToList()
                    .ForEach(fun id -> mangaService.ArchiveAll(id) |> Async.AwaitTask |> Async.RunSynchronously)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            let fromChapter = args.TryGetResult(ArchiveArgs.From_Chapter)
            let toChapter = args.TryGetResult(ArchiveArgs.To_Chapter)

            let manga = db.Manga.TryFirst(fun m -> m.Title = title)

            match manga with
            | Some m -> mangaService.ArchiveRange(m.Id, fromChapter, toChapter) |> Async.AwaitTask |> Async.RunSynchronously
            | None ->
                logger.LogError("Manga {Title} could not be found", title)
                exit 1

    member this.Unarchive(args: ParseResults<UnarchiveArgs>) =
        match args.Contains(UnarchiveArgs.All), args.TryGetResult(UnarchiveArgs.Title) with
        | true, None ->
            if args.Contains(UnarchiveArgs.From_Chapter) || args.Contains(UnarchiveArgs.To_Chapter) then
                args.Raise("Cannot specify --all in combination with --from-chapter or --to-chapter")
            else
                db.Manga
                    .OrderBy(fun m -> m.Title)
                    .Select(fun m -> m.Id)
                    .ToList()
                    .ForEach(fun id -> mangaService.UnarchiveAll(id) |> Async.AwaitTask |> Async.RunSynchronously)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            let fromChapter = args.TryGetResult(UnarchiveArgs.From_Chapter)
            let toChapter = args.TryGetResult(UnarchiveArgs.To_Chapter)
            let manga = db.Manga.TryFirst(fun m -> m.Title = title)

            match manga with
            | Some m -> mangaService.UnarchiveRange(m.Id, fromChapter, toChapter) |> Async.AwaitTask |> Async.RunSynchronously
            | None ->
                logger.LogError("Manga {Title} could not be found", title)
                exit 1

    member this.Version() = printfn $"%s{versionInfo.Version}"
