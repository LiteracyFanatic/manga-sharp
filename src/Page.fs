module MangaSharp.Page

open System.IO

let private fromFileName (file: string) =
    {
        Name = Path.GetFileNameWithoutExtension(file)
        File = file
    }

let fromDir (dir: string) =
    try
        Directory.GetFiles(dir)
        |> Array.map (fun f -> FileInfo(f).Name)
        |> Array.sort
        |> Array.map fromFileName
        |> Array.toList
    with
    | e ->
        failwithf "%s contains no pages." dir

let private fromChapterTitle (mangaTitle: string) (chapterTitle: string) =
    fromDir (Path.Combine(mangaData, mangaTitle, chapterTitle))

let fromTitle (mangaTitle: string) (chapterTitle: string) (pageTitle: string) =
    let pages = fromChapterTitle mangaTitle chapterTitle
    List.find (fun p -> p.Name = pageTitle) pages
