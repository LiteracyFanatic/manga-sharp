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
    Paper
} from "@mui/material";
import React from "react";
import { Link } from "react-router-dom";

import { MangaGetResponse } from "../Api";

interface MangaListItemProps {
    manga: MangaGetResponse
}

function MangaListItem(props: MangaListItemProps) {
    const [anchorEl, setAnchorEl] = React.useState<null | HTMLElement>(null);

    const secondaryText = `(${props.manga.ChapterIndex + 1}/${props.manga.NumberOfChapters}) ${props.manga.Direction}`;

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
                    to={props.manga.BookmarkUrl}
                    divider={true}
                >
                    <ListItemText
                        primary={props.manga.Title}
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
                    href={props.manga.SourceUrl}
                >
                    View Source
                </MenuItem>
            </Menu>
        </Box>
    );
}

interface MangaListProps {
    manga: MangaGetResponse[]
}

export default function MangaList(props: MangaListProps) {
    return (
        <Paper>
            <List>
                {props.manga.map(m => <MangaListItem key={m.Id} manga={m} />)}
            </List>
        </Paper>
    );
}

