import { CircularProgress } from '@mui/material';
import { Image } from 'mui-image';
import React, { useState } from 'react';
import { useTimeoutFn } from 'react-use';

interface LazyImageProps {
    src: string;
    width: number;
    height: number;
    wrapperStyle?: React.CSSProperties;
    fit?: React.CSSProperties['objectFit'];
}

export default function LazyImage(props: LazyImageProps) {
    const [isCached, setIsCached] = useState(true);
    const [isLoaded, setIsLoaded] = useState(false);

    useTimeoutFn(() => {
        if (!isLoaded) {
            setIsCached(false);
        }
    }, 200);

    return (
        <Image
            src={props.src}
            wrapperStyle={props.wrapperStyle}
            fit={props.fit}
            showLoading={isCached ? undefined : <CircularProgress />}
            duration={isCached ? 0 : undefined}
            onLoad={() => setIsLoaded(true)}
            width={props.width}
            height={props.height}
        />
    );
}
