import useSWR, { SWRConfiguration, SWRResponse } from "swr";
import * as z from "zod";

const directionSchema = z.enum(["Horizontal", "Vertical"]);
export type Direction = z.infer<typeof directionSchema>;

const mangaGetResponseSchema = z.object({
    Id: z.string(),
    Title: z.string(),
    BookmarkUrl: z.string(),
    NumberOfChapters: z.number(),
    ChapterIndex: z.number(),
    Direction: directionSchema,
    SourceUrl: z.string().url(),
    Updated: z.coerce.date(),
});

export type MangaGetResponse = z.infer<typeof mangaGetResponseSchema>;

type DownloadStatus =
    | "NotDownloaded"
    | "Downloaded"
    | "Archived"
    | "Ignored"

export interface ChapterGetResponse {
    MangaId: string
    Direction: Direction
    ChapterId: string
    ChapterTitle: string | null
    PreviousChapterUrl: string | null
    NextChapterUrl: string | null
    OtherChapters: {
        Id: string
        Title: string
        Url: string
        DownloadStatus: DownloadStatus
    }[]
    DownloadStatus: DownloadStatus
    Pages: {
        Id: string
        Name: string
        Width: number
        Height: number
    }[]
}

export interface PutBookmarkRequest {
    ChapterId: string
    PageId: string | null
}

export function useApi() {
    async function getManga() {
        const res = await fetch("/api/manga");
        try {
            return mangaGetResponseSchema.array().parse(await res.json());
        } catch (e) {
            console.error(e);
            return [];
        }
    }

    async function getChapter(chapterId: string) {
        const res = await fetch(`/api/chapters/${chapterId}`);
        return await res.json() as Promise<ChapterGetResponse>;
    }

    async function setBookmark(mangaId: string, request: PutBookmarkRequest) {
        await fetch(`/api/manga/${mangaId}/bookmark`, {
            method: "PUT",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(request)
        });
    }

    return {
        getManga,
        getChapter,
        setBookmark
    };
}

export function useManga(config?: SWRConfiguration): SWRResponse<MangaGetResponse[], Error> {
    const { getManga } = useApi();
    return useSWR(["useManga"], getManga, config);
}

export function useChapter(chapterId: string, config?: SWRConfiguration): SWRResponse<ChapterGetResponse, Error> {
    const { getChapter } = useApi();
    return useSWR(["useChapter", chapterId], () => getChapter(chapterId), config);
}
