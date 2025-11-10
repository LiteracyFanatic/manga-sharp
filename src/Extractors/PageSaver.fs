namespace MangaSharp.Extractors

open System.Collections.Generic
open System.IO
open Microsoft.Extensions.Logging
open SixLabors.ImageSharp
open SixLabors.ImageSharp.Processing
open SixLabors.ImageSharp.PixelFormats
open MangaSharp.Extractors.Util
open MangaSharp.Database
open FSharp.Control

module private PageSaver =
    let private maxWebpDimension = 16383

    let private saveImageAsWebpAsync (logger: ILogger) (path: string) (image: Image) =
        task {
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
            use! img = Image.LoadAsync(imageStream)
            let! width, height = saveImageAsWebpAsync logger imagePath img

            return
                Page(
                    Name = Path.GetFileNameWithoutExtension(imagePath),
                    File = imagePath,
                    Width = width,
                    Height = height
                )
        }

    let saveSlicedPagesAsync (logger: ILogger) (mangaTitle: string) (chapterTitle: string) (imageStreams: IAsyncEnumerable<Stream>) =
        task {
            let folder = Path.Combine(mangaData, mangaTitle, chapterTitle)
            Directory.CreateDirectory(folder) |> ignore

            let stitchThreshold = 0.6
            let results = ResizeArray<Page>()
            let mutable previousImage: Image = null
            let mutable i = 0
            let mutable previousIndex = 0

            try
                for stream in imageStreams do
                    use! img = Image.LoadAsync<Rgba32>(stream)

                    if isNull previousImage then
                        previousImage <- img.Clone()
                        previousIndex <- i
                    else
                        if previousImage.Width = img.Width && float img.Height < float previousImage.Height * stitchThreshold then
                            logger.LogDebug(
                                "Stitching image {Idx} (h={CurrH}) to bottom of previous image {PrevIdx} (h={PrevH}) because height is below threshold.",
                                i,
                                img.Height,
                                previousIndex,
                                previousImage.Height
                            )

                            let newWidth = max previousImage.Width img.Width
                            let newHeight = previousImage.Height + img.Height
                            use stitched = new Image<Rgba32>(newWidth, newHeight)

                            stitched.Mutate(fun ctx ->
                                ctx.DrawImage(previousImage, Point(0, 0), 1.0f) |> ignore
                                ctx.DrawImage(img, Point(0, previousImage.Height), 1.0f) |> ignore)

                            previousImage.Dispose()
                            previousImage <- stitched.Clone()
                        else
                            let path = getImagePath folder previousIndex
                            let! w, h = saveImageAsWebpAsync logger path previousImage

                            results.Add(
                                Page(Name = Path.GetFileNameWithoutExtension(path), File = path, Width = w, Height = h)
                            )

                            previousImage.Dispose()
                            previousImage <- img.Clone()
                            previousIndex <- i

                    i <- i + 1

                if not (isNull previousImage) then
                    let path = getImagePath folder previousIndex
                    let! w, h = saveImageAsWebpAsync logger path previousImage
                    results.Add(Page(Name = Path.GetFileNameWithoutExtension(path), File = path, Width = w, Height = h))

                return results |> Seq.toList
            finally
                if not (isNull previousImage) then
                    previousImage.Dispose()
        }

type PageSaver(logger: ILogger<PageSaver>) =
    member this.SavePageAsync(mangaTitle: string, chapterTitle: string, imageNumber: int, imageStream: Stream) =
        PageSaver.savePageAsync logger mangaTitle chapterTitle imageNumber imageStream

    member this.SaveSlicedPagesAsync(mangaTitle: string, chapterTitle: string, imageStreams: IAsyncEnumerable<Stream>) =
        PageSaver.saveSlicedPagesAsync logger mangaTitle chapterTitle imageStreams
