namespace MangaSharp.CLI

open System.Collections.Generic
open System.Linq
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open Argu
open MangaSharp.Database
open MangaSharp.Database.MangaDomain
open MangaSharp.Extractors
open MangaSharp.CLI.Arguments
open MangaSharp.CLI.Util
open Giraffe

type LsJson = {
    Title: string
    Direction: Direction
    NumberOfChapters: int
    BookmarkChapter: string option
    BookmarkPage: string option
    BookmarkUrl: string option
    FirstPageUrl: string
}

type Application(
    db: MangaContext,
    extractors: IEnumerable<IMangaExtractor>,
    jsonSerializer: Json.ISerializer,
    logger: ILogger<Application>) =

    let ensureExtractors (urls: string list) =
        let foundExtractors, errors =
            List.foldBack (fun url (foundExtractors, errors) ->
                match extractors |> Seq.tryFind (fun extractor -> extractor.IsMatch(url)) with
                | Some m -> m :: foundExtractors, errors
                | None -> foundExtractors, url :: errors
            ) urls ([], [])
        match errors with
        | [] -> Ok foundExtractors
        | _ -> Error errors

    let makeDirectionUrlPairs (args: DownloadArgs list) =
        let rec loop direction acc args =
            match args with
            | [] -> acc
            | Direction d :: t -> loop d acc t
            | Url u :: t -> loop direction ((direction, u) :: acc) t

        loop Horizontal [] args
        |> List.rev

    let lsPlain () =
        let manga =
            db.Manga
                .AsNoTracking()
                .Include("_BookmarkChapter")
                .Include("_BookmarkPage")
                .Include(fun m -> m.Chapters)
                .OrderBy(fun m -> m.Title)
                .ToList()
                |> List.ofSeq
        manga
        |> List.iter (fun m ->
            let bookmarkText =
                m.BookmarkChapter
                |> Option.map (fun c ->
                    let pageText =
                        m.BookmarkPage
                        |> Option.map (fun p -> sprintf " Page %s" p.Name)
                        |> Option.defaultValue ""
                    $"Chapter %s{c.Title.Value}%s{pageText}")
                |> Option.defaultValue "None"
            let chapterCount =
                m.Chapters
                    .Where(fun c -> c.DownloadStatus = Downloaded)
                    .Count()
            printfn $"%s{m.Title},%A{m.Direction},%i{chapterCount},%s{bookmarkText}")

    let lsJson () =
        let manga =
            db.Manga
                .AsNoTracking()
                .Include("_BookmarkChapter")
                .Include("_BookmarkPage")
                .Include(fun m -> m.Chapters)
                .OrderBy(fun m -> m.Title)
                .ToList()
                |> List.ofSeq
                |> List.map (fun m ->
                    let chapters =
                        m.Chapters
                            .Where(fun c -> c.DownloadStatus = Downloaded)
                            .OrderBy(fun c -> c.Index)
                            .ToList()
                    let firstChapter = chapters.First()
                    {
                        Title = m.Title
                        Direction = m.Direction
                        NumberOfChapters = Seq.length chapters
                        BookmarkChapter = m.BookmarkChapter |> Option.map (fun c -> c.Title.Value)
                        BookmarkPage = m.BookmarkPage |> Option.map (fun p -> p.Name)
                        BookmarkUrl = Some (getBookmarkUrl m)
                        FirstPageUrl = $"/chapters/%A{firstChapter.Id}/%s{slugify m.Title}/%s{firstChapter.Title.Value}"
                    })
        let json = jsonSerializer.SerializeToString(manga)
        printfn $"%s{json}"

    member this.Download(args: ParseResults<DownloadArgs>) =
        if not (args.Contains(Url)) then
            args.Raise("Argument --url is required")

        let directions, urls =
            args.GetAllResults()
            |> makeDirectionUrlPairs
            |> List.unzip

        match ensureExtractors urls with
        | Ok extractors ->
            List.zip3 urls directions extractors
            |> List.iter (fun (u, d, e) ->
                let res =
                    e.DownloadAsync(u, d)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                match res with
                | Ok _ -> ()
                | Error e ->
                    logger.LogError(e)
                    exit 1)
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
            match args.Contains(All), args.TryGetResult(UpdateArgs.Title) with
            | true, None -> allManga
            | true, _ ->
                args.Raise("Cannot specify --all and a manga title at the same time")
            | false, None ->
                args.Raise("Either --all or a manga title must be specified")
            | false, Some title ->
                match allManga |> List.tryFind (fun m -> m.Title = title) with
                | Some m -> [m]
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
                    extractor.UpdateAsync(m.Id)
                    |> Async.AwaitTask
                    |> Async.RunSynchronously
                | None ->
                    logger.LogError("Could not find a provider for the following URL: {Url}", m.Url)
                    exit 1)
            |> List.contains (Ok(true))
        if not updated then
            logger.LogInformation("No updates were found")

    member this.Ls(args: ParseResults<LsArgs>) =
        if args.Contains(Json) then
            lsJson ()
        else
            lsPlain ()
