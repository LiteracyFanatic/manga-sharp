module MangaSharp.Util

open System
open System.Net
open System.Threading
open System.Collections
open System.Collections.Generic
open System.IO
open FSharp.Data

let rec retry (n: int) (ms: int) (f: unit -> 'T option) =
    match f () with
    | Some x ->
        Some x
    | None ->
        if n = 0 then
            None
        else
            Thread.Sleep(ms)
            retry (n - 1) ms f

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

let downloadFileAsync (path: string) (url: string) =
    async {
        use wc = new WebClient()
        try
            do! wc.AsyncDownloadFile(Uri(url), path)
            return Some ()
        with
        | _ ->
            printfn "Could not download %s." url
            return None
    }

module File =
        
    let tryReadAllText (path: string) =
        try
            Some (File.ReadAllText(path).Trim())
        with
        | :? FileNotFoundException ->
            printfn "Could not find %s." path
            None
        | :? UnauthorizedAccessException ->
            printfn "Could not read %s. Check permissions." path
            None
        | :? IOException ->
            printfn "Could not read %s." path
            None

module HtmlDocument =

    let tryParse (text: string) =
        try
            Some (HtmlDocument.Parse text)
        with
        | _ ->
            printfn "Could not parse HTML."
            None

    let tryLoad (url: string) =
        try
            use wc = new WebClient()
            let text = wc.DownloadString(url)
            tryParse text
        with
        | :? WebException ->
            printfn "Could not download %s." url
            None

type NonEmptyList<'T> =
    private { List: 'T list }

    interface IEnumerable<'T> with
        member this.GetEnumerator() =
            (Seq.ofList this.List).GetEnumerator()

    interface IEnumerable with
        member this.GetEnumerator() =
            (Seq.ofList this.List).GetEnumerator() :> IEnumerator

[<RequireQualifiedAccess>]
module NonEmptyList =

    let tryCreate list =
        match list with
        | [] -> None
        | _ -> Some { List = list }

    let findIndex f { List = list } = List.findIndex f list

    let tryItem a { List = list } = List.tryItem a list

    let tryFind f { List = list } = List.tryFind f list

    let find f { List = list } = List.find f list

    let last { List = list } = List.last list

    let head { List = list } = List.head list

    let length { List = list } = List.length list
