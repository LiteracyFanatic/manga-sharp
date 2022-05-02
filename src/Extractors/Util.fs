module MangaSharp.Extractors.Util

open System
open type Environment
open System.IO
open System.Text.RegularExpressions
open System.Net.Http
open System.Runtime.InteropServices
open System.Diagnostics
open FSharp.Data
open FsToolkit.ErrorHandling

let saveStreamToFileAsync (path: string) (downloadStream: Stream) =
    task {
        use fileStream = new FileStream(path, FileMode.Create)
        do! downloadStream.CopyToAsync(fileStream)
    }

let urlToFilePath (chapterFolder: string) (url: string) (i: int) =
    let ext = Path.GetExtension(Uri(url).LocalPath)
    Path.ChangeExtension(Path.Combine(chapterFolder, $"%03i{i + 1}"), ext)

let tryDownloadStringAsync (hc: HttpClient) (url: string) =
    task {
        try
            let! res = hc.GetStringAsync(url)
            return Ok res
        with
        | _ ->
            return Error ""
    }

module HtmlDocument =

    let tryParse (text: string) =
        try
            Ok (HtmlDocument.Parse text)
        with
        | _ ->
            Error "Could not parse HTML document"

    let tryLoadAsync (hc: HttpClient) (url: string) =
        task {
            try
                let! text = hc.GetStringAsync(url)
                return tryParse text
            with
            | :? HttpRequestException ->
                return Error $"Could not download %s{url}"
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
            FileName=cmd,
            Arguments=args,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true)
    Process.Start(startInfo)

let querySelector (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        Error $"%s{cssQuery} did not match any elements"
    | [node] -> Ok node
    | _ ->
        Error $"%s{cssQuery} matched multiple elements"

let querySelectorAll (html: HtmlDocument) (cssQuery: string) =
    match html.CssSelect(cssQuery) with
    | [] ->
        Error $"%s{cssQuery} didn't match any elements"
    | elements -> Ok elements

let regexMatch (regex: Regex) (input: string) =
    let m = regex.Match(input)
    if m.Success then
        let groups = seq m.Groups
        match Seq.tryItem 1 groups with
        | Some group ->
            match group.Value.Trim() with
            | "" ->
                Error $"%A{regex} returned an empty match"
            | value -> Ok value
        | None ->
            Error $"%A{regex} capture group 1 did not exist"
    else
        Error $"%A{regex} did not match"

let cssAndRegex (cssQuery: string) (regex: Regex) (html: HtmlDocument) =
    result {
        let! node = querySelector html cssQuery
        let text = HtmlNode.directInnerText node
        let! matchingText = regexMatch regex text
        return matchingText.Trim()
    }

let resolveUrl (baseUrl: string) (url: string) =
    Uri(Uri(baseUrl), url).AbsoluteUri

let extractChapterUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    result {
        let! chapters = querySelectorAll html cssQuery
        let urls =
            chapters
            |> Seq.rev
            |> Seq.map (HtmlNode.attributeValue "href" >> resolveUrl url)
            |> Seq.distinct
        return urls
    }

let extractImageUrls (cssQuery: string) = fun (url: string) (html: HtmlDocument) ->
    result {
        let! nodes = querySelectorAll html cssQuery
        return Seq.map (HtmlNode.attributeValue "src" >> resolveUrl url) nodes
    }

let dataHome = Environment.GetFolderPath(SpecialFolder.LocalApplicationData)
let mangaData = Path.Combine(dataHome, "manga")
