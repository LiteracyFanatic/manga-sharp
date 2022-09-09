module MangaSharp.CLI.Util

open System.Text
open System.Text.RegularExpressions
open System.Globalization
open MangaSharp.Database.MangaDomain

let private normalize (form: NormalizationForm) (input: string) =
    input.Normalize(form)

// Converts characters such as "ā" to "a". This is not a correct romanization of
// Japanese text (e.g. "obā-san" becomes "oba-san" when it should really be
// "obaa-san"), but it is better than stripping the character from the URL
// altogether. Unfortunately we don't have enough information to represent the
// elongations correctly without the original kana; depending on the word, "ō"
// may be a rendering of "oo" or "ou".
let private stripDiacritics (input: string) =
    input
    |> normalize NormalizationForm.FormD
    |> String.filter (fun c -> CharUnicodeInfo.GetUnicodeCategory(c) <> UnicodeCategory.NonSpacingMark)
    |> normalize NormalizationForm.FormC

let private replace (pattern: string) (replacement: string) (input: string) =
    Regex.Replace(input, pattern, replacement)

let slugify (input: string) =
    input.Trim().ToLowerInvariant()
    |> stripDiacritics
    |> replace "\s+" "-"
    |> replace "[^a-zA-Z0-9\-\._]" ""
    |> replace "-+" "-"

let getFirstPage (manga: Manga) =
    let chapter =
        manga.Chapters
        |> Seq.sortBy (fun c -> c.Index)
        |> Seq.find (fun c -> c.DownloadStatus = Downloaded)
    $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title.Value}"

let tryPreviousChapter (manga: Manga) (chapter: Chapter) =
    let chapters =
        manga.Chapters
        |> Seq.filter (fun c -> c.DownloadStatus = Downloaded || c.DownloadStatus = Archived)
        |> Seq.sortBy (fun c -> c.Index)
    let i = chapters |> Seq.findIndex (fun c -> c.Id = chapter.Id)
    Seq.tryItem (i - 1) chapters

let tryNextChapter (manga: Manga) (chapter: Chapter) =
    let chapters =
        manga.Chapters
        |> Seq.filter (fun c -> c.DownloadStatus = Downloaded || c.DownloadStatus = Archived)
        |> Seq.sortBy (fun c -> c.Index)
    let i = chapters |> Seq.findIndex (fun c -> c.Id = chapter.Id)
    Seq.tryItem (i + 1) chapters

let getBookmarkUrl (manga: Manga) =
    match manga.BookmarkChapter, manga.BookmarkPage with
    | Some chapter, Some page ->
        $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title.Value}?page=%s{page.Name}"
    | Some chapter, None ->
        $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title.Value}"
    | None, None
    | None, Some _ ->
        let chapter =
            manga.Chapters
            |> Seq.sortBy (fun c -> c.Index)
            |> Seq.find (fun c -> c.DownloadStatus = Downloaded)
        $"/chapters/%A{chapter.Id}/%s{slugify manga.Title}/%s{chapter.Title.Value}"
