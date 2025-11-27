namespace MangaSharp.CLI

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Logging
open MangaSharp.Extractors
open MangaSharp.Database
open FsToolkit.ErrorHandling

type MangaDownloaderService(extractors: IEnumerable<IMangaExtractor>, db: MangaContext, logger: ILogger<MangaDownloaderService>) =

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

    member _.Download(url: string, direction: Direction, ?progress: IProgress<ExtractorProgress>) = task {
        match ensureExtractors [url] with
        | Ok [extractor] ->
            try
                let! res = extractor.DownloadAsync(url, direction, ?progress=progress)
                match res with
                | Ok job ->
                    match! job.Completion with
                    | Ok _ -> return Ok ()
                    | Error e ->
                        logger.LogError("Error downloading {Url}: {Error}", url, e)
                        return Error (e.ToString())
                | Error e ->
                    logger.LogError("Error downloading {Url}: {Error}", url, e)
                    return Error (e.ToString())
            with e ->
                logger.LogError(e, "Exception downloading {Url}", url)
                return Error e.Message
        | Ok _ ->
            return Error "Unexpected extractor count"
        | Error _ ->
            logger.LogError("No extractor found for {Url}", url)
            return Error "No extractor found"
    }

    member _.Update(mangaId: Guid, ?progress: IProgress<ExtractorProgress>) = task {
        try
            let! manga = db.Manga.FindAsync(mangaId)
            if isNull (box manga) then
                return Error "Manga not found"
            else
                match ensureExtractors [manga.Url] with
                | Ok [extractor] ->
                    let! res = extractor.UpdateAsync(mangaId, ?progress=progress)
                    match res with
                    | Ok job ->
                        match! job.Completion with
                        | Ok _ -> return Ok ()
                        | Error e -> return Error (e.ToString())
                    | Error e -> return Error (e.ToString())
                | _ -> return Error "No extractor found"
        with e -> return Error e.Message
    }

    member _.CheckForUpdates(mangaId: Guid, url: string) = task {
        match ensureExtractors [url] with
        | Ok [extractor] ->
            try
                let! res = extractor.CheckForUpdatesAsync(mangaId)
                match res with
                | Ok count -> return Ok count
                | Error e -> return Error (e.ToString())
            with e -> return Error e.Message
        | _ -> return Error "No extractor found"
    }

    member _.GetExtractorName(url: string) =
        extractors
        |> Seq.tryFind (fun e -> e.IsMatch(url))
        |> Option.map (fun e -> e.Name)
