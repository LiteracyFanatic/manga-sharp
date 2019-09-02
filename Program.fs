open MangaSharp

[<EntryPoint>]
let main argv =
    let indexUrl = argv.[0]
    let direction =
        match argv.[1] with
        | "horizontal" -> Horizontal
        | "vertical" -> Vertical
    let manga = {
        Url = indexUrl
        Direction = direction
        Provider = Provider.tryFromTable indexUrl direction |> Option.get
    }
    Manga.download manga
    0 // return an integer exit code
