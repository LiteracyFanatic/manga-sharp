import { Home } from "@mui/icons-material";
import {
    AppBar,
    Button,
    MenuItem,
    Stack,
    TextField,
    Toolbar
} from "@mui/material";
import { Link } from "react-router-dom";

import { ChapterGetResponse } from "../Api";
import HideOnScroll from "../components/HideOnScroll";

interface ReadingPageAppBarProps {
    chapter: ChapterGetResponse
    currentPageIndex: number
    onChangeChapterSelect: React.ChangeEventHandler<HTMLInputElement>
    onChangePageSelect: React.ChangeEventHandler<HTMLInputElement>
}

export default function ReadingPageAppBar(props: ReadingPageAppBarProps) {
    return (
        <HideOnScroll>
            <AppBar
                sx={{
                    opacity: 0.9
                }}
            >
                <Toolbar>
                    <Stack
                        direction="row"
                        gap={1}
                    >
                        <Button
                            variant="outlined"
                            color="inherit"
                            component={Link}
                            to="/"
                        >
                            <Home />
                        </Button>
                        <TextField
                            size="small"
                            select
                            value={props.chapter.ChapterId}
                            onChange={props.onChangeChapterSelect}
                        >
                            {props.chapter.OtherChapters.map(chapter => (
                                <MenuItem
                                    key={chapter.Id}
                                    value={chapter.Id}
                                    disabled={chapter.DownloadStatus === "Archived"}
                                >
                                    {chapter.DownloadStatus === "Archived" ? `Chapter ${chapter.Title} (Archived)` : `Chapter ${chapter.Title}`}
                                </MenuItem>
                            ))}
                        </TextField>
                        {props.chapter.Direction === "Horizontal" && props.currentPageIndex >= 0 &&
                            <TextField
                                select
                                size="small"
                                value={props.chapter.Pages[props.currentPageIndex].Id}
                                onChange={props.onChangePageSelect}
                            >
                                {props.chapter.Pages.map(page => (
                                    <MenuItem key={page.Id} value={page.Id}>
                                        {`Page ${parseInt(page.Name)}`}
                                    </MenuItem>
                                ))}
                            </TextField>}
                    </Stack>
                </Toolbar>
            </AppBar>
        </HideOnScroll>
    );
}

