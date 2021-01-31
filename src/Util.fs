module MangaSharp.Util

open System
open System.IO
open System.Net.Http
open FSharp.Data

let rec retryAsync (n: int) (ms: int) (f: unit -> Async<'T option>) =
    async {
        match! f () with
        | Some x ->
            return Some x
        | None ->
            if n = 0 then
                return None
            else
                do! Async.Sleep(ms)
                return! retryAsync (n - 1) ms f
    }

let private hc = lazy new HttpClient(Timeout=TimeSpan.FromSeconds(20.))

let downloadFileAsync (path: string) (req: HttpRequestMessage) =
    async {
        try
            let! res = hc.Force().SendAsync(req) |> Async.AwaitTask
            let! downloadStream = res.Content.ReadAsStreamAsync() |> Async.AwaitTask
            use fileStream = new FileStream(path, FileMode.Create)
            do! downloadStream.CopyToAsync(fileStream) |> Async.AwaitTask
            return Some ()
        with
        | _ ->
            return None
    }

let tryDownloadStringAsync (url: string) =
    async {
        try
            let! res = hc.Force().GetStringAsync(url) |> Async.AwaitTask
            return Some res
        with
        | _ ->
            return None
    }

module HtmlDocument =

    let tryParse (text: string) =
        try
            Some (HtmlDocument.Parse text)
        with
        | _ ->
            printfn "Could not parse HTML."
            None

    let tryLoadAsync (url: string) =
        async {
            try
                let! text = hc.Force().GetStringAsync(url) |> Async.AwaitTask
                return tryParse text
            with
            | :? HttpRequestException ->
                printfn "Could not download %s." url
                return None
        }

module List =
    let mapAt (i: int) (f: 'a -> 'a) (list: 'a list) =
        List.mapi (fun n x -> if n = i then f x else x) list
