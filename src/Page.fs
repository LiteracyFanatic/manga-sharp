module MangaSharp.Page

open MangaSharp
open MangaSharp.Util
open System.IO

let private fromFileName (file: string) =
    {
        Name = Path.GetFileNameWithoutExtension(file)
        File = file
    }

let fromDir (dir: string) =
    try
        let pages =
            Directory.GetFiles(dir)
            |> Array.map (fun f -> FileInfo(f).Name)
            |> Array.sort
            |> Array.map fromFileName
            |> Array.toList
        NonEmptyList.create pages
    with
    | e ->
        failwithf "%s contains no pages." dir

let private fromChapterTitle (mangaTitle: string) (chapterTitle: string) =
    let dir = Path.Combine(mangaData, mangaTitle, chapterTitle)
    fromDir dir

let fromTitle (mangaTitle: string) (chapterTitle: string) (pageTitle: string) =
    let pages = fromChapterTitle mangaTitle chapterTitle
    NonEmptyList.find (fun p -> p.Name = pageTitle) pages
