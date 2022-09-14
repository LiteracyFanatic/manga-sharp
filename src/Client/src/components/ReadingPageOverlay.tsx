import { ChevronLeft, ChevronRight } from "@mui/icons-material";
import {
    Box,
    Stack,
    SxProps,
    Theme,
    Typography
} from "@mui/material";

interface ReadingPageOverlayProps {
    onClickPrevious?: () => void
    onClickNext?: () => void
    previousDisabled?: boolean
    nextDisabled?: boolean
    sx?: SxProps<Theme>
}

export default function ReadingPageOverlay(props: ReadingPageOverlayProps) {
    return (
        <Stack
            direction="row"
            sx={props.sx}
        >
            <Box
                onClick={props.previousDisabled ? undefined : props.onClickPrevious}
                sx={{
                    height: "100%",
                    width: "calc(100%/3)",
                    cursor: props.previousDisabled ? undefined : "pointer",
                    opacity: 0,
                    "@media (hover: hover) and (pointer: fine)": {
                        "&:hover": {
                            opacity: 1
                        }
                    },
                    transition: "opacity 0.5s",
                    display: "flex",
                    flexDirection: "column",
                    justifyContent: "center",
                    alignItems: "start",
                    color: theme => props.previousDisabled ? theme.palette.grey[700] : undefined,
                    WebkitTapHighlightColor: "transparent"
                }}
            >
                <Box
                    sx={{
                        marginLeft: 3
                    }}
                >
                    <ChevronLeft
                        sx={{
                            fontSize: 48
                        }}
                    />
                    <Typography
                        textAlign="center"
                        sx={{
                            marginY: "-0.5rem"
                        }}
                    >
                        PREV
                    </Typography>
                </Box>
            </Box>
            <Box
                sx={{
                    height: "100%",
                    flexGrow: 1
                }}
            />
            <Box
                onClick={props.nextDisabled ? undefined : props.onClickNext}
                sx={{
                    height: "100%",
                    width: "calc(100%/3)",
                    cursor: "pointer",
                    opacity: 0,
                    "@media (hover: hover) and (pointer: fine)": {
                        "&:hover": {
                            opacity: 1
                        }
                    },
                    transition: "opacity 0.5s",
                    display: "flex",
                    flexDirection: "column",
                    justifyContent: "center",
                    alignItems: "end",
                    color: theme => props.nextDisabled ? theme.palette.grey[700] : undefined,
                    WebkitTapHighlightColor: "transparent"
                }}
            >
                <Box
                    sx={{
                        marginRight: 3
                    }}
                >
                    <ChevronRight
                        sx={{
                            fontSize: 48
                        }}
                    />
                    <Typography
                        textAlign="center"
                        sx={{
                            marginY: "-0.5rem"
                        }}
                    >
                        NEXT
                    </Typography>
                </Box>
            </Box>
        </Stack>
    );
}

