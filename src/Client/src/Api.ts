import { useAsyncFn } from "react-use";
import useSWR, { SWRConfiguration, SWRResponse, useSWRConfig } from "swr";
import * as z from "zod";

type UnwrapSWR<T> = T extends SWRResponse<infer Data> ? Data : never;
type UnwrapSWRHook<T extends (...args: any[]) => any> = UnwrapSWR<ReturnType<T>>;

const directionSchema = z.enum(["Horizontal", "Vertical"]);
export type Direction = z.infer<typeof directionSchema>;

const mangaGetResponseSchema = z.object({
    Id: z.string(),
    Title: z.string(),
    BookmarkUrl: z.string(),
    NumberOfChapters: z.object({
        NotDownloaded: z.number(),
        Downloaded: z.number(),
        Archived: z.number(),
        Ignored: z.number(),
        Total: z.number()
    }),
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

export async function deleteManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}`, { method: "DELETE" });
}

export async function archiveManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}/archive`, { method: "POST" });
}

export async function unarchiveManga(mangaId: string) {
    await fetch(`/api/manga/${mangaId}/unarchive`, { method: "POST" });
}

export async function setMangaDirection(mangaId: string, direction: Direction) {
    await fetch(`/api/manga/${mangaId}/direction`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ Direction: direction })
    });
}

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

export async function setBookmark(mangaId: string, request: PutBookmarkRequest) {
    await fetch(`/api/manga/${mangaId}/bookmark`, {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(request)
    });
}

export function useManga(config?: SWRConfiguration): SWRResponse<MangaGetResponse[], Error> {
    return useSWR(["useManga"], getManga, config);
}

export function useChapter(chapterId: string, config?: SWRConfiguration): SWRResponse<ChapterGetResponse, Error> {
    return useSWR(["useChapter", chapterId], () => getChapter(chapterId), config);
}

export function useDeleteManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate<UnwrapSWRHook<typeof useManga>>(["useManga"], async (current) => {
            await deleteManga(mangaId);
            return (current ?? []).filter(manga => manga.Id !== mangaId);
        }, {
            revalidate: false,
            optimisticData: (currentData) => (currentData ?? []).filter(manga => manga.Id !== mangaId)
        });
    });
    
    return { ...state, trigger };
}

export function useArchiveManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate(["useManga"], archiveManga(mangaId));
    });

    return { ...state, trigger };
}

export function useUnarchiveManga() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string) => {
        await mutate(["useManga"], unarchiveManga(mangaId));
    });

    return { ...state, trigger };
}

export function useSetMangaDirection() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string, direction: Direction) => {
        await mutate<UnwrapSWRHook<typeof useManga>>(["useManga"], async (current) => {
            await setMangaDirection(mangaId, direction);
            return (current ?? []).map(manga => {
                if (manga.Id === mangaId) {
                    return { ...manga, Direction: direction };
                }
                return manga;
            });
        }, {
            revalidate: false,
            optimisticData: (currentData) => (currentData ?? []).map(manga => {
                if (manga.Id === mangaId) {
                    return { ...manga, Direction: direction };
                }
                return manga;
            })
        });
    });

    return { ...state, trigger };
}

export function useSetBookmark() {
    const { mutate } = useSWRConfig();

    const [state, trigger] = useAsyncFn(async (mangaId: string, request: PutBookmarkRequest) => {
        await mutate(["useManga"], async (current) => {
            await setBookmark(mangaId, request);
            return current;
        });
    });

    return { ...state, trigger };
}
