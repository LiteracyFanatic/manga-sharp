import { Close, Search } from "@mui/icons-material";
import {
    alpha,
    AppBar,
    Box,
    IconButton,
    InputBase,
    Toolbar
} from "@mui/material";
import { useEffect, useState } from "react";
import { useDebounce } from "react-use";

interface IndexAppBarProps {
    defaultValue?: string
    onChange?: (value: string) => void
}

export default function IndexAppBar(props: IndexAppBarProps) {
    const [searchText, setSearchText] = useState(props.defaultValue || "");
    const [searchTextDebounced, setSearchTextDebounced] = useState(searchText);
    useDebounce(
        () => setSearchTextDebounced(searchText),
        500,
        [searchText]
    );

    useEffect(() => {
        if (props.onChange) {
            props.onChange(searchTextDebounced);
        }
    }, [searchTextDebounced]);

    function onChangeSearchText(value: string) {
        setSearchText(value);
        if (!value) {
            setSearchTextDebounced(value);
        }
    }

    function onClickClear() {
        setSearchText("");
        setSearchTextDebounced("");
    }

    return (
        <Box>
            <AppBar>
                <Toolbar
                    sx={{
                        justifyContent: "end"
                    }}
                >
                    <Box
                        sx={theme => ({
                            display: "flex",
                            borderRadius: 1,
                            backgroundColor: alpha(theme.palette.common.white, 0.15),
                            "&:hover": {
                                backgroundColor: alpha(theme.palette.common.white, 0.25)
                            },
                            width: "100%",
                            [theme.breakpoints.up("sm")]: {
                                marginLeft: theme.spacing(3),
                                width: "auto"
                            }
                        })}
                    >
                        <Box
                            sx={{
                                display: "flex",
                                justifyContent: "center",
                                alignItems: "center",
                                padding: 1
                            }}
                        >
                            <Search />
                        </Box>
                        <InputBase
                            value={searchText}
                            onChange={e => onChangeSearchText(e.target.value)}
                            sx={{
                                marginLeft: 1,
                                flexGrow: 1
                            }}
                            placeholder="Search"
                        />
                        <IconButton
                            onClick={onClickClear}
                            disabled={!searchText}
                        >
                            <Close />
                        </IconButton>
                    </Box>
                </Toolbar>
            </AppBar>
            <Toolbar />
        </Box>
    );
}

