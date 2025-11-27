import { Drawer, List, ListItemButton, ListItemText, Toolbar, useMediaQuery, useTheme } from '@mui/material';
import { useLocation, useNavigate } from 'react-router-dom';

import { drawerWidth } from '../constants';
import { useDrawer } from '../hooks/useDrawer';

export default function AppDrawer() {
    const { isOpen, closeDrawer } = useDrawer();
    const navigate = useNavigate();
    const location = useLocation();
    const theme = useTheme();
    const isDesktop = useMediaQuery(theme.breakpoints.up('md'));

    if (location.pathname.startsWith('/chapters/')) {
        return null;
    }

    function handleNavigate(path: string) {
        if (location.pathname !== path) {
            navigate(path);
        }
        if (!isDesktop) {
            closeDrawer();
        }
    }

    return (
        <Drawer
            variant={isDesktop ? 'permanent' : 'temporary'}
            open={isDesktop || isOpen}
            onClose={closeDrawer}
            sx={{
                'width': isDesktop ? drawerWidth : undefined,
                'flexShrink': 0,
                '& .MuiDrawer-paper': {
                    width: drawerWidth,
                    boxSizing: 'border-box'
                }
            }}
        >
            <Toolbar />
            <List>
                <ListItemButton
                    onClick={() => handleNavigate('/')}
                    selected={location.pathname === '/'}
                >
                    <ListItemText primary="Manga" />
                </ListItemButton>
                <ListItemButton
                    onClick={() => handleNavigate('/downloads')}
                    selected={location.pathname === '/downloads'}
                >
                    <ListItemText primary="Downloads" />
                </ListItemButton>
            </List>
        </Drawer>
    );
}
