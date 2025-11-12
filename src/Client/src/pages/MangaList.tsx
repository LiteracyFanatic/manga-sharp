import { MoreHoriz } from "@mui/icons-material";
import {
    Box,
    IconButton,
    ListItem,
    ListItemButton,
    ListItemText,
    Menu,
    MenuItem,
    Paper,
    SxProps,
    Theme
} from "@mui/material";
import React from "react";
import { Link } from "react-router-dom";

import { MangaGetResponse, useArchiveManga, useDeleteManga, useSetMangaDirection, useUnarchiveManga } from "../Api";
import { useConfirm } from "material-ui-confirm";
import { Virtuoso } from "react-virtuoso";

interface MangaListItemProps {
    manga: Fuzzysort.KeysResult<MangaGetResponse>
}

function MangaListItem(props: MangaListItemProps) {
    const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
    const confirm = useConfirm();
    const deleteManga = useDeleteManga();
    const archiveManga = useArchiveManga();
    const unarchiveManga = useUnarchiveManga();
    const setMangaDirection = useSetMangaDirection();

    const disabled = deleteManga.loading || archiveManga.loading || unarchiveManga.loading || setMangaDirection.loading;

    async function handleDelete() {
        setAnchorEl(null);
        await confirm({ title: "Delete manga?", description: "This will permanently remove the manga and all chapters." });
        await deleteManga.trigger(props.manga.obj.Id);
    }

    async function handleArchive() {
        setAnchorEl(null);
        await confirm({ title: "Archive manga?", description: "All downloaded chapters will be marked archived." });
        await archiveManga.trigger(props.manga.obj.Id);
    }

    async function handleUnarchive() {
        setAnchorEl(null);
        await confirm({ title: "Unarchive manga?", description: "All archived chapters will be restored to downloaded." });
        await unarchiveManga.trigger(props.manga.obj.Id);
    }

    async function handleDirectionChange() {
        setAnchorEl(null);
        const nextDirection = props.manga.obj.Direction === "Horizontal" ? "Vertical" : "Horizontal";
        await setMangaDirection.trigger(props.manga.obj.Id, nextDirection);
    }

    const highlightedTitle = props.manga[0].highlight((m, i) => (
        <Box
            key={i}
            component="span"
            sx={theme => ({
                fontWeight: theme.typography.fontWeightBold,
                backgroundColor: theme.palette.primary.main,
                color: theme => theme.palette.primary.contrastText
            })}
        >
            {m}
        </Box>
    ));
    const title = highlightedTitle?.length ? highlightedTitle : props.manga.obj.Title;

    let location = "";
    if (props.manga.obj.NumberOfChapters.Downloaded > 0) {
        location = `${props.manga.obj.ChapterIndex + 1}/${props.manga.obj.NumberOfChapters.Downloaded}`
    } else if (props.manga.obj.NumberOfChapters.Archived > 0) {
        location = "archived";
    } else if (props.manga.obj.NumberOfChapters.NotDownloaded > 0) {
        location = "pending download";
    }

    const secondaryText = `(${location}) ${props.manga.obj.Direction} ${props.manga.obj.Updated.toLocaleDateString()}`;

    return (
        <Box
        >
            <ListItem
                disablePadding
                secondaryAction={
                    <IconButton
                        edge="end"
                        onClick={(e: React.MouseEvent<HTMLButtonElement>) => setAnchorEl(e.currentTarget)}
                    >
                        <MoreHoriz />
                    </IconButton>}
            >
                <ListItemButton
                    disabled={!props.manga.obj.NumberOfChapters.Downloaded}
                    component={Link}
                    to={props.manga.obj.BookmarkUrl}
                    divider={true}
                >
                    <ListItemText
                        primary={title}
                        secondary={secondaryText}
                    />
                </ListItemButton>
            </ListItem>
            <Menu
                anchorEl={anchorEl}
                open={Boolean(anchorEl)}
                onClose={() => setAnchorEl(null)}
            >
                <MenuItem
                    component="a"
                    LinkComponent="a"
                    href={props.manga.obj.SourceUrl}
                >
                    View Source
                </MenuItem>
                <MenuItem disabled={disabled} onClick={handleDelete}>Delete</MenuItem>
                {props.manga.obj.NumberOfChapters.Downloaded > 0 && (
                    <MenuItem disabled={disabled} onClick={handleArchive}>Archive</MenuItem>
                )}
                {props.manga.obj.NumberOfChapters.Archived > 0 && (
                    <MenuItem disabled={disabled} onClick={handleUnarchive}>Unarchive</MenuItem>
                )}
                <MenuItem disabled={disabled} onClick={handleDirectionChange}>Change Direction</MenuItem>
            </Menu>
        </Box>
    );
}

interface MangaListProps {
    manga: Fuzzysort.KeysResult<MangaGetResponse>[]
    sx?: SxProps<Theme>
}

export default function MangaList(props: MangaListProps) {
    return (
        <Paper sx={props.sx}>
            <Virtuoso
                useWindowScroll
                totalCount={props.manga.length}
                itemContent={i => <MangaListItem manga={props.manga[i]} />}

            />
        </Paper>
    );
}
