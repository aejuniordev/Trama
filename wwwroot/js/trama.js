// Ponte mínima entre o navegador e os componentes C#.
// Converte eventos de ponteiro para coordenadas do SVG e limita as chamadas ao servidor.
window.trama = (() => {
    function toSvgPoint(svg, event) {
        const matrix = svg.getScreenCTM();
        if (!matrix) return null;
        return new DOMPoint(event.clientX, event.clientY).matrixTransform(matrix.inverse());
    }

    function attachCanvas(svg, dotnet) {
        let busy = false;
        let queued = null;
        let down = false;

        // Enquanto uma chamada de movimento está em andamento, guarda só a posição mais recente.
        const flush = async () => {
            if (busy || !queued) return;
            busy = true;
            const q = queued;
            queued = null;
            try {
                await dotnet.invokeMethodAsync('OnPointerMove', q.x, q.y, q.shift);
            } catch (_) { /* circuito encerrado */ }
            busy = false;
            if (queued) flush();
        };

        const onDown = (e) => {
            if (e.button !== 0) return;
            const p = toSvgPoint(svg, e);
            if (!p) return;
            down = true;
            queued = null;
            try { svg.setPointerCapture(e.pointerId); } catch (_) { }
            e.preventDefault();
            dotnet.invokeMethodAsync('OnPointerDown', p.x, p.y, e.shiftKey).catch(() => { });
        };

        const onMove = (e) => {
            const p = toSvgPoint(svg, e);
            if (!p) return;
            queued = { x: p.x, y: p.y, shift: e.shiftKey };
            flush();
        };

        const onUp = (e) => {
            if (!down) return;
            down = false;
            try { svg.releasePointerCapture(e.pointerId); } catch (_) { }
            const p = toSvgPoint(svg, e);
            if (!p) return;
            queued = null;
            dotnet.invokeMethodAsync('OnPointerUp', p.x, p.y, e.shiftKey).catch(() => { });
        };

        const onLeave = () => {
            if (down) return;
            queued = null;
            dotnet.invokeMethodAsync('OnPointerLeave').catch(() => { });
        };

        svg.style.touchAction = 'none';
        svg.addEventListener('pointerdown', onDown);
        svg.addEventListener('pointermove', onMove);
        svg.addEventListener('pointerup', onUp);
        svg.addEventListener('pointercancel', onUp);
        svg.addEventListener('pointerleave', onLeave);

        return {
            dispose() {
                svg.removeEventListener('pointerdown', onDown);
                svg.removeEventListener('pointermove', onMove);
                svg.removeEventListener('pointerup', onUp);
                svg.removeEventListener('pointercancel', onUp);
                svg.removeEventListener('pointerleave', onLeave);
            }
        };
    }

    function attachKeys(dotnet) {
        const send = (action) => dotnet.invokeMethodAsync('OnShortcut', action).catch(() => { });

        const onKey = (e) => {
            const t = e.target;
            const typing = t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable);
            const ctrl = e.ctrlKey || e.metaKey;
            const key = (e.key || '').toLowerCase();

            if (ctrl && key === 's') {
                e.preventDefault();
                // Tira o foco para que o campo em edição dispare "change" antes de salvar.
                if (document.activeElement && document.activeElement.blur) document.activeElement.blur();
                send('save');
                return;
            }

            if (typing) return;

            if (ctrl && key === 'z') { e.preventDefault(); send(e.shiftKey ? 'redo' : 'undo'); return; }
            if (ctrl && key === 'y') { e.preventDefault(); send('redo'); return; }
            if (ctrl || e.altKey) return;

            const map = {
                'delete': 'delete',
                'backspace': 'delete',
                'escape': 'escape',
                'v': 'tool-select',
                'l': 'tool-line',
                'e': 'tool-erase',
            };
            const action = map[key];
            if (action) {
                e.preventDefault();
                send(action);
            }
        };

        document.addEventListener('keydown', onKey);
        return { dispose() { document.removeEventListener('keydown', onKey); } };
    }

    function downloadText(filename, text) {
        const blob = new Blob([text], { type: 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    async function copyText(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch (_) {
            return false;
        }
    }

    return { attachCanvas, attachKeys, downloadText, copyText };
})();
