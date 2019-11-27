module MangaSharp.Direction

open MangaSharp

let tryParse (direction: string) =
    match direction with
    | "horizontal" ->
        Some Horizontal
    | "vertical" ->
        Some Vertical
    | _ ->
        printfn "%s does not contain a valid direction." direction
        None
