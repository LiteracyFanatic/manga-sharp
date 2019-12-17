module MangaSharp.Page

open MangaSharp
open MangaSharp.Util
open System.IO
open Giraffe.ComputationExpressions

let private fromFileName (file: string) =
    {
        Name = Path.GetFileNameWithoutExtension(file)
        File = file
    }

let fromDir (dir: string) =
    let pages =
        Directory.GetFiles(dir)
        |> Array.map (fun f -> FileInfo(f).Name)
        |> Array.sort
        |> Array.map fromFileName
        |> Array.toList
    match NonEmptyList.tryCreate pages with
    | Some pages ->
        Some pages
    | None ->
        printfn "%s contains no pages." dir
        None

let private fromChapterTitle (mangaTitle: string) (chapterTitle: string) =
    let dir = Path.Combine(mangaData, mangaTitle, chapterTitle)
    fromDir dir

let tryFromTitle (mangaTitle: string) (chapterTitle: string) (pageTitle: string) =
    opt {
        let! pages = fromChapterTitle mangaTitle chapterTitle
        return! NonEmptyList.tryFind (fun p -> p.Name = pageTitle) pages
    }
