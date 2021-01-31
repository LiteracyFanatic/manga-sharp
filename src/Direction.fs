module MangaSharp.Direction

let parse (direction: string) =
    match direction with
    | "horizontal" ->
        Horizontal
    | "vertical" ->
        Vertical
    | _ ->
        failwithf "%s does not contain a valid direction." direction
