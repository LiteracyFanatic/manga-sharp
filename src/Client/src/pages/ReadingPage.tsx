import { Box, CircularProgress, Container, Typography } from "@mui/material";
import React, { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useKey } from "react-use";

import { ChapterGetResponse, useApi, useChapter } from "../Api";
import ReadingPageAppBar from "../components/ReadingPageAppBar";
import ReadingPageOverlay from "../components/ReadingPageOverlay";
import ReadingPageStepper from "../components/ReadingPageStepper";
import { useSearchParam } from "../hooks/useSearchParam";
import LazyImage from "./LazyImage";

export default function ReadingPage() {
    const { chapterId } = useParams();
    const [pageName, setPageName] = useSearchParam("page");
    const [innerHeight, setInnerHeight] = useState(window.innerHeight);
    const navigate = useNavigate();
    const { setBookmark } = useApi();

    const chapter = useChapter(chapterId || "");
    const currentPageIndex = chapter.data ? chapter.data.Pages.findIndex(page => page.Name === pageName) : -1;

    const progressBarHeight = 4;

    useEffect(() => {
        if (chapter.data?.Direction === "Horizontal") {
            if (!pageName) {
                setPageName("001");
            }
            if (currentPageIndex >= 0) {
                setBookmark(chapter.data.MangaId, {
                    ChapterId: chapter.data.ChapterId,
                    PageId: chapter.data.Pages[currentPageIndex].Id
                });
            }
        }
    }, [chapter.data, pageName, setPageName, currentPageIndex]);

    useEffect(() => {
        if (chapter.data?.Direction === "Vertical") {
            setBookmark(chapter.data.MangaId, {
                ChapterId: chapter.data.ChapterId,
                PageId: null
            });
        }
    }, [chapter.data]);

    function onClickPrevious(chapter: ChapterGetResponse) {
        if (chapter.Direction === "Horizontal" && currentPageIndex > 0) {
            setPageName(chapter.Pages[currentPageIndex - 1].Name);
        } else if (chapter.PreviousChapterUrl) {
            navigate(chapter.PreviousChapterUrl);
        }
    }

    function onClickNext(chapter: ChapterGetResponse) {
        if (chapter.Direction === "Horizontal" && currentPageIndex >= 0 && currentPageIndex < chapter.Pages.length - 1) {
            setPageName(chapter.Pages[currentPageIndex + 1].Name);
        } else if (chapter.NextChapterUrl) {
            navigate(chapter.NextChapterUrl);
        }
    }

    function onChangeStep(page: number) {
        if (chapter.data) {
            setPageName(chapter.data.Pages[page].Name);
        }
    }

    useKey(e => e.key === "ArrowLeft",
        e => chapter.data && onClickPrevious(chapter.data),
        undefined,
        [chapter]);
    useKey(e => e.key === "ArrowRight" || e.key === " ",
        e => chapter.data && onClickNext(chapter.data),
        undefined,
        [chapter]);

    function onChangeChapterSelect(e: React.ChangeEvent<HTMLInputElement>) {
        if (chapter.data) {
            const selectedChapter = chapter.data.OtherChapters.find(chapter => chapter.Id === e.target.value);
            if (selectedChapter) {
                navigate(selectedChapter.Url);
            }
        }
    }

    function onChangePageSelect(e: React.ChangeEvent<HTMLInputElement>) {
        if (chapter.data) {
            const selectedPage = chapter.data.Pages.find(page => page.Id === e.target.value);
            if (selectedPage) {
                setPageName(selectedPage.Name);
            }
        }
    }

    useEffect(() => {
        const handler = () => setInnerHeight(window.innerHeight);
        window.addEventListener("resize", handler);
        return () => window.removeEventListener("resize", handler);
    }, []);

    function getContent() {
        if (chapter.data) {
            const chapterData = chapter.data;
            if (chapterData.DownloadStatus === "Archived") {
                return (
                    <Typography>
                        {chapterData.ChapterTitle ? `Chapter ${chapterData.ChapterTitle} is archived.` : "Chapter is archived."}
                    </Typography>
                );
            } else {
                const getWrapperStyles = (i: number): React.CSSProperties => {
                    if (chapterData.Direction === "Horizontal") {
                        return {
                            // Hide other images by setting width to 0 so that CSS animation doesn't trigger when they are revealed
                            width: i === currentPageIndex ? "auto" : 0,
                            maxHeight: i === currentPageIndex ? innerHeight - progressBarHeight : 0
                        };
                    } else {
                        return {
                            height: "auto",
                            width: "100%",
                            maxWidth: "800px"
                        };
                    }
                };
                return (
                    <>
                        <ReadingPageAppBar
                            chapter={chapterData}
                            currentPageIndex={currentPageIndex}
                            onChangeChapterSelect={onChangeChapterSelect}
                            onChangePageSelect={onChangePageSelect}
                        />
                        <Box
                            sx={{
                                width: "100%",
                                maxWidth: "100vw",
                                height: innerHeight,
                                userSelect: "none",
                                display: "flex",
                                justifyContent: chapterData.Direction === "Horizontal" ? "center" : "start",
                                flexDirection: "column"
                            }}
                        >
                            <Box
                                sx={{
                                    width: "100%",
                                    display: "flex",
                                    flexDirection: "column",
                                    alignItems: "center"
                                }}
                            >
                                <Box
                                    sx={{
                                        position: "fixed",
                                        top: theme => theme.mixins.toolbar.minHeight,
                                        left: 0,
                                        right: 0,
                                        bottom: chapterData.Direction === "Horizontal" ? progressBarHeight : 0,
                                        zIndex: 2
                                    }}
                                >
                                    <ReadingPageOverlay
                                        onClickPrevious={() => onClickPrevious(chapterData)}
                                        onClickNext={() => onClickNext(chapterData)}
                                        previousDisabled={!chapterData.PreviousChapterUrl && currentPageIndex <= 0}
                                        nextDisabled={!chapterData.NextChapterUrl && (currentPageIndex === -1 || currentPageIndex === chapterData.Pages.length - 1)}
                                        sx={{
                                            width: "100%",
                                            height: "100%"
                                        }}
                                    />
                                </Box>
                                {chapterData.Pages.map((page, i) => (
                                    <LazyImage
                                        key={page.Id}
                                        src={`/pages/${page.Id}`}
                                        wrapperStyle={getWrapperStyles(i)}
                                        fit={chapterData.Direction === "Horizontal" ? "contain" : "cover"}
                                        width={page.Width}
                                        height={page.Height}
                                    />
                                ))}
                            </Box>
                            {chapterData.Direction === "Horizontal" &&
                                <ReadingPageStepper
                                    currentPage={currentPageIndex}
                                    numberOfPages={chapterData.Pages.length}
                                    onChangeStep={onChangeStep}
                                    sx={{
                                        width: "100%",
                                        height: progressBarHeight
                                    }}
                                />}
                        </Box>
                    </>
                );
            }
        } else if (chapter.isValidating) {
            return (
                <Container
                    sx={{
                        display: "flex",
                        justifyContent: "center",
                        alignItems: "center",
                        height: innerHeight
                    }}
                    maxWidth="md"
                >
                    <CircularProgress />
                </Container>
            );
        } else {
            return <Typography>Chapter not found.</Typography>;
        }
    }

    return getContent();
}
