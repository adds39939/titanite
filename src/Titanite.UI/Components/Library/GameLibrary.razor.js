export function watchGridColumns(element, owner) {
    let columns = 0;

    const measure = () => {
        const row = element.querySelector('.game-grid-row');

        if (!row) {
            return;
        }

        const count = getComputedStyle(row).gridTemplateColumns.split(' ').filter(Boolean).length;

        if (count > 0 && count !== columns) {
            columns = count;
            owner.invokeMethodAsync('SetColumns', count);
        }
    };

    const resizes = new ResizeObserver(measure);
    const additions = new MutationObserver(measure);

    resizes.observe(element);
    additions.observe(element, { childList: true });

    return {
        dispose: () => {
            resizes.disconnect();
            additions.disconnect();
        }
    };
}
