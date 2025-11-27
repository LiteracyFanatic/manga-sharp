import React, { createContext, type ReactNode, useContext, useState } from 'react';

type DrawerContextValue = {
    isOpen: boolean;
    openDrawer: () => void;
    closeDrawer: () => void;
};

const DrawerContext = createContext<DrawerContextValue | undefined>(undefined);

type DrawerProviderProps = {
    children: ReactNode;
};

export function DrawerProvider(props: DrawerProviderProps): ReactNode {
    const [isOpen, setIsOpen] = useState<boolean>(false);

    const openDrawer = (): void => {
        setIsOpen(true);
    };

    const closeDrawer = (): void => {
        setIsOpen(false);
    };

    return (
        <DrawerContext.Provider
            value={{
                isOpen,
                openDrawer,
                closeDrawer
            }}
        >
            {props.children}
        </DrawerContext.Provider>
    );
}

export function useDrawer(): DrawerContextValue {
    const context = useContext(DrawerContext);

    if (context === undefined) {
        throw new Error('useDrawer must be used within a DrawerProvider');
    }

    return context;
}
