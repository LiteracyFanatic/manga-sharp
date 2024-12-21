module MangaSharp.Extractors.Util

open System
open System.Threading.Tasks
open type Environment
open System.IO
open System.Text.RegularExpressions
open System.Net.Http
open System.Runtime.InteropServices
open System.Diagnostics
open FSharp.Data
open FsToolkit.ErrorHandling

let private dataHome = Environment.GetFolderPath(SpecialFolder.LocalApplicationData)
let mangaData = Path.Combine(dataHome, "manga")

let tryDownloadStringAsync (hc: HttpClient) (url: string) =
    hc.GetStringAsync(url) |> TaskResult.ofTask |> TaskResult.catch id

module HtmlDocument =

    let tryParse (text: string) =
        try
            Ok(HtmlDocument.Parse text)
        with e ->
            Error e

    type TryLoadAsyncError =
        | DownloadError of exn
        | ParseError of exn

    let tryLoadAsync (hc: HttpClient) (url: string) : Task<Result<HtmlDocument, TryLoadAsyncError>> =
        taskResult {
            let! text = tryDownloadStringAsync hc url |> TaskResult.mapError DownloadError
            return! tryParse text |> Result.mapError ParseError
        }

let openInDefaultApp (url: string) =
    let cmd, args =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            "cmd", $"/c start \"%s{url}\""
        else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            "xdg-open", $"\"%s{url}\""
        else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            "open", $"\"%s{url}\""
        else
            failwith "Unrecognized platform"

    let startInfo =
        ProcessStartInfo(
            FileName = cmd,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        )

    Process.Start(startInfo)

type QuerySelectorError =
    | NoMatch of cssQuery: string
    | MultipleMatches of cssQuery: string

let querySelector (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] -> Error(NoMatch cssQuery)
    | [ node ] -> Ok node
    | _ -> Error(MultipleMatches cssQuery)

type QuerySelectorAllError = NoMatch of cssQuery: string

let querySelectorAll (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] -> Error(NoMatch cssQuery)
    | elements -> Ok elements

type RegexMatchError =
    | NoMatch of Regex
    | EmptyMatch of Regex

let regexMatch (regex: Regex) (input: string) =
    let m = regex.Match(input)

    if m.Success then
        let groups = seq m.Groups

        match Seq.tryItem 1 groups with
        | Some group ->
            match group.Value.Trim() with
            | "" -> Error(EmptyMatch regex)
            | value -> Ok value
        | None -> Error(NoMatch regex)
    else
        Error(NoMatch regex)

let resolveUrl (baseUrl: string) (url: string) = Uri(Uri(baseUrl), url).AbsoluteUri

let extractChapterUrls (cssQuery: string) =
    fun (url: string) (html: HtmlDocument) ->
        result {
            let! chapters = querySelectorAll html cssQuery

            let urls =
                chapters
                |> Seq.rev
                |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
                |> Seq.distinct

            return urls
        }

let extractImageUrls (cssQuery: string) =
    fun (url: string) (html: HtmlDocument) ->
        result {
            let! nodes = querySelectorAll html cssQuery
            return Seq.map (HtmlNode.attributeValue "src" >> resolveUrl url) nodes
        }

type IMangaSharpError =
    interface
    end

type CommonError =
    | ParseError of exn
    | TryLoadAsyncError of HtmlDocument.TryLoadAsyncError
    | QuerySelectorError of QuerySelectorError
    | QuerySelectorAllError of QuerySelectorAllError
    | RegexMatchError of RegexMatchError
    | Other of exn

    interface IMangaSharpError
    static member Map(e: HtmlDocument.TryLoadAsyncError) = TryLoadAsyncError e
    static member Map(e: QuerySelectorError) = QuerySelectorError e
    static member Map(e: QuerySelectorAllError) = QuerySelectorAllError e
    static member Map(e: RegexMatchError) = RegexMatchError e
    static member Map(e: exn) = Other e
    static member FromResult(e: Result<'a, HtmlDocument.TryLoadAsyncError>) = e |> Result.mapError CommonError.Map
    static member FromResult(e: Result<'a, QuerySelectorError>) = e |> Result.mapError CommonError.Map
    static member FromResult(e: Result<'a, QuerySelectorAllError>) = e |> Result.mapError CommonError.Map
    static member FromResult(e: Result<'a, RegexMatchError>) = e |> Result.mapError CommonError.Map
    static member FromResult(e: Result<'a, exn>) = e |> Result.mapError CommonError.Map

    static member FromTaskResult(e: TaskResult<'a, HtmlDocument.TryLoadAsyncError>) =
        e |> TaskResult.mapError CommonError.Map

    static member FromTaskResult(e: TaskResult<'a, QuerySelectorError>) =
        e |> TaskResult.mapError CommonError.Map

    static member FromTaskResult(e: TaskResult<'a, QuerySelectorAllError>) =
        e |> TaskResult.mapError CommonError.Map

    static member FromTaskResult(e: TaskResult<'a, RegexMatchError>) =
        e |> TaskResult.mapError CommonError.Map

    static member FromTaskResult(e: TaskResult<'a, exn>) =
        e |> TaskResult.mapError CommonError.Map
