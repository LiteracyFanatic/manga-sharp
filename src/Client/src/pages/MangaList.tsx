import { MoreHoriz } from "@mui/icons-material";
import {
    Box,
    IconButton,
    List,
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

import { MangaGetResponse } from "../Api";

interface MangaListItemProps {
    manga: Fuzzysort.KeysResult<MangaGetResponse>
}

function MangaListItem(props: MangaListItemProps) {
    const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);

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

    const secondaryText = `(${props.manga.obj.ChapterIndex + 1}/${props.manga.obj.NumberOfChapters}) ${props.manga.obj.Direction} ${props.manga.obj.Updated.toLocaleDateString()}`;

    return (
        <Box>
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
            <List>
                {props.manga.map(m => <MangaListItem key={m.obj.Id} manga={m} />)}
            </List>
        </Paper>
    );
}
