import { MoreHoriz } from '@mui/icons-material';
import {
    Box,
    IconButton,
    ListItem,
    ListItemButton,
    ListItemText,
    ListSubheader,
    Menu,
    MenuItem,
    Paper,
    SxProps,
    Theme
} from '@mui/material';
import { formatDistanceToNow } from 'date-fns';
import { useConfirm } from 'material-ui-confirm';
import { useSnackbar } from 'notistack';
import React from 'react';
import { Link } from 'react-router-dom';
import { Virtuoso } from 'react-virtuoso';

import {
    MangaGetResponse,
    useArchiveManga,
    useCheckUpdate,
    useDeleteManga,
    useSetMangaDirection,
    useUnarchiveManga
} from '../Api';

interface DateHeaderItem {
    type: 'header';
    date: string;
}

interface MangaItem {
    type: 'manga';
    manga: Fuzzysort.KeysResult<MangaGetResponse>;
}

type GroupedMangaItem = DateHeaderItem | MangaItem;

function groupMangaByDate(manga: Fuzzysort.KeysResult<MangaGetResponse>[]): GroupedMangaItem[] {
    const items: GroupedMangaItem[] = [];
    let currentDateKey: string | null = null;

    for (const m of manga) {
        const dateKey = formatDistanceToNow(m.obj.Updated, { addSuffix: true });
        if (dateKey !== currentDateKey) {
            items.push({ type: 'header', date: dateKey });
            currentDateKey = dateKey;
        }
        items.push({ type: 'manga', manga: m });
    }

    return items;
}

interface MangaListItemProps {
    manga: Fuzzysort.KeysResult<MangaGetResponse>;
}

function MangaListItem(props: MangaListItemProps) {
    const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);
    const confirm = useConfirm();
    const deleteManga = useDeleteManga();
    const archiveManga = useArchiveManga();
    const unarchiveManga = useUnarchiveManga();
    const setMangaDirection = useSetMangaDirection();
    const checkUpdate = useCheckUpdate();
    const { enqueueSnackbar } = useSnackbar();

    const disabled = deleteManga.loading || archiveManga.loading || unarchiveManga.loading || setMangaDirection.isMutating || checkUpdate.isMutating;

    async function handleDelete() {
        setAnchorEl(null);
        await confirm({ title: 'Delete manga?', description: 'This will permanently remove the manga and all chapters.' });
        await deleteManga.trigger(props.manga.obj.Id);
    }

    async function handleArchive() {
        setAnchorEl(null);
        await confirm({ title: 'Archive manga?', description: 'All downloaded chapters will be marked archived.' });
        await archiveManga.trigger(props.manga.obj.Id);
    }

    async function handleUnarchive() {
        setAnchorEl(null);
        await confirm({ title: 'Unarchive manga?', description: 'All archived chapters will be restored to downloaded.' });
        await unarchiveManga.trigger(props.manga.obj.Id);
    }

    async function handleDirectionChange() {
        setAnchorEl(null);
        const nextDirection = props.manga.obj.Direction === 'Horizontal' ? 'Vertical' : 'Horizontal';
        await setMangaDirection.trigger({ MangaId: props.manga.obj.Id, Direction: nextDirection });
    }

    async function handleCheckUpdate() {
        setAnchorEl(null);
        try {
            const result = await checkUpdate.trigger(props.manga.obj.Id);
            if (result.Count > 0) {
                enqueueSnackbar(`${result.Count.toString()} new chapter(s) available`, { variant: 'info' });
            }
            else {
                enqueueSnackbar('No updates found', { variant: 'info' });
            }
        }
        catch (e) {
            console.error(e);
            enqueueSnackbar('Failed to check for updates', { variant: 'error' });
        }
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
    const title = highlightedTitle.length ? highlightedTitle : props.manga.obj.Title;

    let location = '';
    if (props.manga.obj.NumberOfChapters.Downloaded > 0) {
        location = `${(props.manga.obj.ChapterIndex + 1).toString()}/${props.manga.obj.NumberOfChapters.Downloaded.toString()}`;
    }
    else if (props.manga.obj.NumberOfChapters.Archived > 0) {
        location = 'archived';
    }
    else if (props.manga.obj.NumberOfChapters.NotDownloaded > 0) {
        location = 'pending download';
    }

    const secondaryText = `(${location}) ${props.manga.obj.Direction} ${props.manga.obj.Updated.toLocaleDateString()}`;

    return (
        <Box>
            <ListItem
                disablePadding
                secondaryAction={(
                    <IconButton
                        edge="end"
                        onClick={(e: React.MouseEvent<HTMLButtonElement>) => setAnchorEl(e.currentTarget)}
                    >
                        <MoreHoriz />
                    </IconButton>
                )}
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
                <MenuItem disabled={disabled} onClick={handleCheckUpdate}>Check for Updates</MenuItem>
            </Menu>
        </Box>
    );
}

interface MangaListProps {
    manga: Fuzzysort.KeysResult<MangaGetResponse>[];
    groupByDate?: boolean;
    sx?: SxProps<Theme>;
}

export default function MangaList(props: MangaListProps) {
    if (props.groupByDate) {
        const groupedItems = groupMangaByDate(props.manga);
        return (
            <Paper sx={props.sx}>
                <Virtuoso
                    useWindowScroll
                    totalCount={groupedItems.length}
                    itemContent={(i) => {
                        const item = groupedItems[i];
                        if (item.type === 'header') {
                            return <ListSubheader>{item.date}</ListSubheader>;
                        }
                        return <MangaListItem manga={item.manga} />;
                    }}
                />
            </Paper>
        );
    }

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
