import {
    LinearProgress,
    Step,
    Stepper,
    SxProps,
    Theme
} from '@mui/material';
import { useEffect, useState } from 'react';

interface ReadingPageStepperProps {
    currentPage: number;
    numberOfPages: number;
    onChangeStep?: (step: number) => void;
    sx?: SxProps<Theme>;
}

export default function ReadingPageStepper(props: ReadingPageStepperProps) {
    const [innerWidth, setInnerWidth] = useState(window.innerWidth);

    useEffect(() => {
        const handler = () => setInnerWidth(window.innerHeight);
        window.addEventListener('resize', handler);
        return () => window.removeEventListener('resize', handler);
    }, []);

    const stepWidth = (innerWidth - 2 * (props.numberOfPages - 1)) / props.numberOfPages;

    function onChangeStep(step: number) {
        if (props.onChangeStep) {
            props.onChangeStep(step);
        }
    }

    if (stepWidth < 4) {
        return (
            <LinearProgress
                variant="determinate"
                value={100 * (props.currentPage + 1) / props.numberOfPages}
                sx={props.sx}
            />
        );
    }
    else {
        return (
            <Stepper
                alternativeLabel
                activeStep={props.currentPage}
                connector={null}
                sx={[
                    {
                        height: '4px',
                        gap: '2px'
                    },
                    ...(Array.isArray(props.sx) ? props.sx : [props.sx] as const)
                ]}
            >
                {Array.from(Array(props.numberOfPages).keys()).map(i => (
                    <Step
                        key={i}
                        onClick={() => onChangeStep(i)}
                        sx={{
                            paddingX: 0,
                            height: '100%',
                            backgroundColor: theme => i <= props.currentPage ? theme.palette.primary.main : theme.palette.grey[800],
                            cursor: 'pointer'
                        }}
                    />
                ))}
            </Stepper>
        );
    }
}
