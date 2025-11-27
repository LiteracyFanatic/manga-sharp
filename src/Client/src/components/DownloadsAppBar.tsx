import MenuIcon from '@mui/icons-material/Menu';
import RefreshIcon from '@mui/icons-material/Refresh';
import { AppBar, IconButton, Toolbar, Tooltip, Typography } from '@mui/material';

import { useCheckAllUpdates } from '../Api';
import { useDrawer } from '../hooks/useDrawer';

export default function DownloadsAppBar() {
    const { openDrawer } = useDrawer();
    const { trigger: checkUpdates, isMutating: isCheckingUpdates } = useCheckAllUpdates();

    return (
        <AppBar
            position="fixed"
            sx={{
                zIndex: theme => theme.zIndex.drawer + 1
            }}
        >
            <Toolbar>
                <IconButton
                    color="inherit"
                    edge="start"
                    onClick={openDrawer}
                    sx={theme => ({
                        mr: 2,
                        [theme.breakpoints.up('md')]: {
                            display: 'none'
                        }
                    })}
                    aria-label="open navigation menu"
                >
                    <MenuIcon />
                </IconButton>
                <Typography variant="h6" noWrap component="div">
                    Downloads
                </Typography>
                <Tooltip title="Check for updates">
                    <IconButton
                        color="inherit"
                        onClick={() => checkUpdates()}
                        disabled={isCheckingUpdates}
                        sx={{ marginLeft: 'auto' }}
                    >
                        <RefreshIcon />
                    </IconButton>
                </Tooltip>
            </Toolbar>
        </AppBar>
    );
}
