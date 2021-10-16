module MangaSharp.Util

open System
open System.IO
open System.Net.Http
open System.Runtime.InteropServices
open System.Diagnostics
open System.Threading.Tasks
open FSharp.Data

let rec retryAsync (n: int) (ms: int) (f: unit -> Task<Result<'T, string>>) =
    task {
        match! f () with
        | Ok x ->
            return Ok x
        | Error e ->
            if n = 0 then
                return Error e
            else
                do! Async.Sleep(ms)
                return! retryAsync (n - 1) ms f
    }

let hc = lazy new HttpClient(Timeout=TimeSpan.FromSeconds(20.))

let downloadFileAsync (path: string) (req: HttpRequestMessage) =
    task {
        try
            let! res = hc.Force().SendAsync(req)
            let! downloadStream = res.Content.ReadAsStreamAsync()
            use fileStream = new FileStream(path, FileMode.Create)
            do! downloadStream.CopyToAsync(fileStream)
            return Ok ()
        with
        | _ ->
            return Error ""
    }

let tryDownloadStringAsync (url: string) =
    task {
        try
            let! res = hc.Force().GetStringAsync(url)
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
            Error "Could not parse HTML document."

    let tryLoadAsync (url: string) =
        task {
            try
                let! text = hc.Force().GetStringAsync(url)
                return tryParse text
            with
            | :? HttpRequestException ->
                return Error $"Could not download %s{url}."
        }

module List =
    let mapAt (i: int) (f: 'a -> 'a) (list: 'a list) =
        List.mapi (fun n x -> if n = i then f x else x) list

module Option =
    let collect f =
        Option.map f >> Option.flatten

let openInDefaultApp (url: string) =
    let cmd, args =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            "cmd", $"/c start \"%s{url}\""
        else if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            "xdg-open", $"\"%s{url}\""
        else if RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            "open", $"\"%s{url}\""
        else
            failwith "Unrecognized platform."
    let startInfo =
        ProcessStartInfo(
            FileName=cmd,
            Arguments=args,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true
        )
    Process.Start(startInfo)
