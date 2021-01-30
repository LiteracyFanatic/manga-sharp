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

let parse (direction: string) =
    match direction with
    | "horizontal" ->
        Horizontal
    | "vertical" ->
        Vertical
    | _ ->
        failwithf "%s does not contain a valid direction." direction
