import { useSearchParams } from 'react-router-dom';

export function useSearchParam<T extends string>(paramName: string): [T | null, (value: T | null) => void];

export function useSearchParam<T extends string>(paramName: string, defaultValue: string): [T, (value: T) => void];

export function useSearchParam<T extends string>(paramName: string, defaultValue?: T) {
    const [searchParams, setSearchParams] = useSearchParams();
    const searchParam = searchParams.get(paramName) || defaultValue || null;

    if (defaultValue) {
        const setSearchParam = (value: T) => {
            setSearchParams({
                ...Object.fromEntries(searchParams.entries()),
                [paramName]: value
            });
        };

        return [searchParam, setSearchParam];
    }
    else {
        const setSearchParam = (value: T | null) => {
            if (value) {
                setSearchParams({
                    ...Object.fromEntries(searchParams.entries()),
                    [paramName]: value
                });
            }
            else {
                setSearchParams(
                    Array.from(searchParams.entries()).filter(([k, _]) => k !== paramName)
                );
            }
        };

        return [searchParam, setSearchParam];
    }
}
