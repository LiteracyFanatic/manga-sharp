namespace MangaSharp.Extractors

open System.IO
open Microsoft.Extensions.Logging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open MangaSharp.Extractors.Util
open MangaSharp.Database

module private PageSaver =

    let private maxWebpDimension = 16383

    let private saveImageAsWebpAsync (logger: ILogger) (path: string) (imageStream: Stream) =
        task {
            use! image = Image.LoadAsync(imageStream)

            if image.Height > maxWebpDimension || image.Width > maxWebpDimension then
                logger.LogDebug(
                    "Image size of {Width}x{Height} exceeds the maximum allowed dimension of {MaxDimension} for the WebP format. Resizing image.",
                    image.Width,
                    image.Height,
                    maxWebpDimension
                )

                image.Mutate(fun image ->
                    let opts = ResizeOptions(Mode = ResizeMode.Max, Size = Size(maxWebpDimension))
                    image.Resize(opts) |> ignore)

                logger.LogDebug(
                    "Finished resizing image. New dimensions are {Width}x{Height}.",
                    image.Width,
                    image.Height
                )

            logger.LogDebug("Converting image to WebP and saving to {Path}.", path)
            do! image.SaveAsWebpAsync(path)
            return image.Width, image.Height
        }

    let private getImagePath (chapterFolder: string) (i: int) =
        Path.ChangeExtension(Path.Combine(chapterFolder, $"%03i{i + 1}"), "webp")

    let savePageAsync
        (logger: ILogger)
        (mangaTitle: string)
        (chapterTitle: string)
        (imageNumber: int)
        (imageStream: Stream)
        =
        task {
            let folder = Path.Combine(mangaData, mangaTitle, chapterTitle)
            Directory.CreateDirectory(folder) |> ignore
            let imagePath = getImagePath folder imageNumber
            let! (width, height) = saveImageAsWebpAsync logger imagePath imageStream

            return
                Page(
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    File = imagePath,
                    Width = width,
                    Height = height
                )
        }

type PageSaver(logger: ILogger<PageSaver>) =
    member this.SavePageAsync(mangaTitle: string, chapterTitle: string, imageNumber: int, imageStream: Stream) =
        PageSaver.savePageAsync logger mangaTitle chapterTitle imageNumber imageStream
