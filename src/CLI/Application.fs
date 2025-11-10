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
        logger: ILogger<Application>
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

    let getDirForMangaSafe (manga: Manga) =
        let dir = Path.Combine(mangaData, manga.Title)
        // Make sure we don't accidentally delete the whole manga directory if manga.Title is somehow empty
        if dir = mangaData then
            logger.LogError("Manga had no title. Refusing to delete {MangaDataDirectory}.", mangaData)
            exit 1
        else
            dir

    // Remove manga from database and associated files from disk
    let rm (manga: Manga) =
        let dir = getDirForMangaSafe manga

        try
            db.Manga.Remove(manga) |> ignore
            db.SaveChanges() |> ignore
            logger.LogInformation("Removed {Title} from database.", manga.Title)

            try
                if Directory.Exists(dir) then
                    Directory.Delete(dir, true)

                logger.LogInformation("Deleted {MangaDirectory}.", dir)
            with e ->
                logger.LogError(e, "Couldn't delete {MangaDirectory}.", dir)
        with e ->
            logger.LogError(e, "Something went wrong while removing {Title} from database.", manga.Title)

    let tryGetChapterByTitleOption (defaultChapter: Chapter) (manga: Manga) (chapterTitle: string option) =
        match chapterTitle with
        | None -> Some defaultChapter
        | Some chapterTitle ->
            match manga.Chapters |> Seq.tryFind (fun c -> c.Title = chapterTitle) with
            | None ->
                logger.LogError("Could not find a chapter with title {ChapterTitle}.", chapterTitle)
                None
            | chapter -> chapter

    let tryGetFromChapterByTitleOption (manga: Manga) (chapterTitle: string option) =
        let defaultChapter = manga.Chapters |> Seq.minBy (fun c -> c.Index)
        tryGetChapterByTitleOption defaultChapter manga chapterTitle

    let tryGetToChapterByTitleOption (manga: Manga) (chapterTitle: string option) =
        let defaultChapter = manga.Chapters |> Seq.maxBy (fun c -> c.Index)
        tryGetChapterByTitleOption defaultChapter manga chapterTitle

    let archiveChapters (manga: Manga) (fromChapter: Chapter) (toChapter: Chapter) =
        let chapters =
            manga.Chapters
            |> Seq.sortBy (fun c -> c.Index)
            |> Seq.filter (fun c -> c.Index >= fromChapter.Index && c.Index <= toChapter.Index)

        if Seq.isEmpty chapters then
            logger.LogError("No matching chapters found for {Title}.", manga.Title)
        else
            for chapter in chapters do
                // Leave chapters with other statuses the same so that we don't try to access null titles in other parts of the code
                if chapter.DownloadStatus = DownloadStatus.Downloaded then
                    chapter.DownloadStatus <- DownloadStatus.Archived

                chapter.Pages.Clear()

            db.SaveChanges() |> ignore
            logger.LogInformation("Marked matching chapters for {Title} as archived in database.", manga.Title)
            let mangaDir = getDirForMangaSafe manga

            for chapter in chapters do
                match Option.ofObj chapter.Title with
                | Some chapterTitle ->
                    let dir = Path.Combine(mangaDir, chapterTitle)

                    try
                        if Directory.Exists(dir) then
                            Directory.Delete(dir, true)

                        logger.LogInformation("Deleted {MangaChapterDirectory}.", dir)
                    with e ->
                        logger.LogError(e, "Couldn't delete {MangaChapterDirectory}.", dir)
                | None -> ()

    // Mark manga chapters as archived in database and remove associated files from disk
    let archive (manga: Manga) (fromChapterTitle: string option) (toChapterTitle: string option) =
        if Seq.isEmpty manga.Chapters then
            logger.LogError("{Title} has no chapters.", manga.Title)
        else
            try
                let fromChapter = tryGetFromChapterByTitleOption manga fromChapterTitle
                let toChapter = tryGetToChapterByTitleOption manga toChapterTitle

                match fromChapter, toChapter with
                | Some fromChapter, Some toChapter -> archiveChapters manga fromChapter toChapter
                | _ -> ()
            with e ->
                logger.LogError(
                    e,
                    "Something went wrong while marking chapters for {Title} as archived in database.",
                    manga.Title
                )

    let unarchiveChapters (manga: Manga) (fromChapter: Chapter) (toChapter: Chapter) =
        let chapters =
            manga.Chapters
            |> Seq.sortBy (fun c -> c.Index)
            |> Seq.filter (fun c -> c.Index >= fromChapter.Index && c.Index <= toChapter.Index)

        if Seq.isEmpty chapters then
            logger.LogError("No matching chapters found for {Title}.", manga.Title)
        else
            for chapter in chapters do
                if chapter.DownloadStatus = DownloadStatus.Archived then
                    chapter.DownloadStatus <- DownloadStatus.NotDownloaded

            db.SaveChanges() |> ignore
            logger.LogInformation("Marked matching chapters for {Title} as not downloaded in database.", manga.Title)

    // Mark archived manga chapters as not downloaded in database
    let unarchive (manga: Manga) (fromChapterTitle: string option) (toChapterTitle: string option) =
        if Seq.isEmpty manga.Chapters then
            logger.LogError("{Title} has no chapters.", manga.Title)
        else
            try
                let fromChapter = tryGetFromChapterByTitleOption manga fromChapterTitle
                let toChapter = tryGetToChapterByTitleOption manga toChapterTitle

                match fromChapter, toChapter with
                | Some fromChapter, Some toChapter -> unarchiveChapters manga fromChapter toChapter
                | _ -> ()
            with e ->
                logger.LogError(
                    e,
                    "Something went wrong while marking chapters for {Title} as not downloaded in database.",
                    manga.Title
                )

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
            db.Manga
                .AsNoTracking()
                .Include(fun m -> m.Chapters)
                .OrderBy(fun m -> m.Title)
                .ToList()
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
        | true, None -> db.Manga.OrderBy(fun m -> m.Title).ToList().ForEach(rm)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            match db.Manga.TryFirst(fun m -> m.Title = title) with
            | Some m -> rm m
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
                    .Include(fun m -> m.Chapters :> IEnumerable<_>)
                    .ThenInclude(fun (c: Chapter) -> c.Pages)
                    .AsSplitQuery()
                    .OrderBy(fun m -> m.Title)
                    .ToList()
                    .ForEach(fun m -> archive m None None)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            let fromChapter = args.TryGetResult(ArchiveArgs.From_Chapter)
            let toChapter = args.TryGetResult(ArchiveArgs.To_Chapter)

            let manga =
                db.Manga
                    .Include(fun m -> m.Chapters :> IEnumerable<_>)
                    .ThenInclude(fun (c: Chapter) -> c.Pages)
                    .AsSplitQuery()
                    .TryFirst(fun m -> m.Title = title)

            match manga with
            | Some m -> archive m fromChapter toChapter
            | None ->
                logger.LogError("Manga {Title} could not be found", title)
                exit 1

    member this.Unarchive(args: ParseResults<UnarchiveArgs>) =
        match args.Contains(UnarchiveArgs.All), args.TryGetResult(UnarchiveArgs.Title) with
        | true, None ->
            if
                args.Contains(UnarchiveArgs.From_Chapter)
                || args.Contains(UnarchiveArgs.To_Chapter)
            then
                args.Raise("Cannot specify --all in combination with --from-chapter or --to-chapter")
            else
                db.Manga
                    .Include(fun m -> m.Chapters)
                    .OrderBy(fun m -> m.Title)
                    .ToList()
                    .ForEach(fun m -> unarchive m None None)
        | true, _ -> args.Raise("Cannot specify --all and a manga title at the same time")
        | false, None -> args.Raise("Either --all or a manga title must be specified")
        | false, Some title ->
            let fromChapter = args.TryGetResult(UnarchiveArgs.From_Chapter)
            let toChapter = args.TryGetResult(UnarchiveArgs.To_Chapter)
            let manga = db.Manga.Include(fun m -> m.Chapters).TryFirst(fun m -> m.Title = title)

            match manga with
            | Some m -> unarchive m fromChapter toChapter
            | None ->
                logger.LogError("Manga {Title} could not be found", title)
                exit 1

    member this.Version() = printfn $"%s{versionInfo.Version}"
