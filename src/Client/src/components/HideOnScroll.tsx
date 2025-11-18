import { Slide, useScrollTrigger } from '@mui/material';

interface HideOnScrollProps {
    children: React.ReactElement;
}

export default function HideOnScroll(props: HideOnScrollProps) {
    const trigger = useScrollTrigger();
    return (
        <Slide
            appear={false}
            direction="down"
            in={!trigger}
        >
            {props.children}
        </Slide>
    );
}
