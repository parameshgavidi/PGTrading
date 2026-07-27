// Candlestick chart renderer for PG One with zoom/pan support.
window.pgOneChart = (function () {
    const states = {};

    function num(v) { return v == null ? null : Number(v); }

    function ensureInteractions(canvas, id) {
        if (canvas.dataset.pgBound === '1') return;
        canvas.dataset.pgBound = '1';

        // Mouse wheel: zoom around the cursor.
        canvas.addEventListener('wheel', function (e) {
            const st = states[id];
            if (!st) return;
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
            zoomAt(id, factor);
        }, { passive: false });

        // Drag to pan.
        let dragging = false, lastX = 0;
        canvas.addEventListener('mousedown', function (e) { dragging = true; lastX = e.clientX; });
        window.addEventListener('mouseup', function () { dragging = false; });
        canvas.addEventListener('mousemove', function (e) {
            if (!dragging) return;
            const st = states[id];
            if (!st) return;
            const perBar = (canvas.clientWidth - 72) / st.count;
            const deltaBars = Math.round((e.clientX - lastX) / perBar);
            if (deltaBars !== 0) {
                st.offset = clamp(st.offset + deltaBars, 0, st.candles.length - st.count);
                lastX = e.clientX;
                render(id);
            }
        });
    }

    function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }

    function zoomAt(id, factor) {
        const st = states[id];
        if (!st) return;
        const newCount = clamp(Math.round(st.count / factor), 12, st.candles.length);
        // Keep the right edge roughly anchored while zooming.
        st.count = newCount;
        st.offset = clamp(st.offset, 0, st.candles.length - st.count);
        render(id);
    }

    function levelColor(level) {
        const g = level.group || '';
        const kind = level.kind || '';
        if (g === 'today') {
            if (kind === 'sell') return 'rgba(255, 82, 82, 0.85)';
            if (kind === 'buy') return 'rgba(0, 200, 83, 0.85)';
            return 'rgba(212, 175, 55, 0.9)';
        }
        if (g === 'prev') return 'rgba(160, 160, 160, 0.65)';
        if (g === 'cam') {
            if (kind === 'sell') return 'rgba(255, 150, 110, 0.75)';
            if (kind === 'buy') return 'rgba(100, 210, 150, 0.75)';
            return 'rgba(190, 190, 230, 0.8)';
        }
        return 'rgba(200, 200, 200, 0.55)';
    }

    function drawLevels(ctx, levels, padding, width, chartH, toY) {
        if (!levels || levels.length === 0) return;

        levels.forEach(function (lv) {
            const price = Number(lv.price);
            if (!price || Number.isNaN(price)) return;

            const y = toY(price);
            const color = levelColor(lv);
            const dash = lv.group === 'prev' ? [5, 4] : [8, 4];

            ctx.strokeStyle = color;
            ctx.lineWidth = lv.group === 'today' ? 1.35 : 1;
            ctx.setLineDash(dash);
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();
            ctx.setLineDash([]);

            ctx.font = 'bold 10px "Segoe UI", sans-serif';
            ctx.textAlign = 'left';
            ctx.fillStyle = color;
            const label = (lv.label || 'Level') + ' ' + price.toFixed(2);
            const textY = y < padding.top + 14 ? y + 14 : y - 5;
            ctx.fillText(label, padding.left + 4, textY);
        });
    }

    function setData(canvasId, candles, timeframe, levels) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        const prev = states[canvasId];
        let count, offset;
        if (prev && prev.timeframe === timeframe) {
            count = clamp(prev.count, 12, candles.length);
            offset = clamp(prev.offset, 0, candles.length - count);
        } else {
            count = candles.length; // show everything by default
            offset = 0;
        }

        states[canvasId] = { canvas, candles, timeframe, count, offset, levels: levels || [] };
        ensureInteractions(canvas, canvasId);
        render(canvasId);
    }

    function zoom(canvasId, factor) { zoomAt(canvasId, factor); }

    function resetZoom(canvasId) {
        const st = states[canvasId];
        if (!st) return;
        st.count = st.candles.length;
        st.offset = 0;
        render(canvasId);
    }

    function render(id) {
        const st = states[id];
        if (!st) return;
        const { canvas, candles, timeframe } = st;

        const container = canvas.parentElement;
        const cssW = Math.max((container ? container.clientWidth : canvas.clientWidth) - 4, 320);
        const cssH = Math.max((container ? container.clientHeight : canvas.clientHeight) - 4, 240);

        // High-DPI crisp rendering.
        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
        canvas.style.width = cssW + 'px';
        canvas.style.height = cssH + 'px';

        const ctx = canvas.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        const width = cssW;
        const height = cssH;
        const padding = { top: 10, right: 52, bottom: 22, left: 6 };
        const chartW = width - padding.left - padding.right;
        const chartH = height - padding.top - padding.bottom;

        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = '#121212';
        ctx.fillRect(0, 0, width, height);

        // Viewport slice
        const start = clamp(candles.length - st.offset - st.count, 0, Math.max(0, candles.length - 1));
        const end = clamp(candles.length - st.offset, 1, candles.length);
        const view = candles.slice(start, end);
        if (view.length === 0) return;

        const collect = (key) => view.map(c => num(c[key])).filter(v => v != null && !Number.isNaN(v));
        const highs = view.map(c => Number(c.high));
        const lows = view.map(c => Number(c.low));
        const levelPrices = (st.levels || []).map(l => Number(l.price)).filter(v => !Number.isNaN(v));
        const extra = [].concat(collect('superTrend'), collect('keltnerUpperOuter'), collect('keltnerLowerOuter'), collect('vwap'));
        const maxPrice = Math.max(...highs, ...(extra.length ? extra : [Number.MIN_VALUE]), ...(levelPrices.length ? levelPrices : [Number.MIN_VALUE]));
        const minPrice = Math.min(...lows, ...(extra.length ? extra : [Number.MAX_VALUE]), ...(levelPrices.length ? levelPrices : [Number.MAX_VALUE]));
        const priceRange = (maxPrice - minPrice) || 1;
        const slot = chartW / view.length;
        const candleWidth = Math.max(1.5, slot - 2);

        const toY = (price) => padding.top + ((maxPrice - price) / priceRange) * chartH;
        const toX = (i) => padding.left + slot * i + slot / 2;

        // Horizontal grid + price labels
        ctx.textBaseline = 'middle';
        for (let i = 0; i <= 4; i++) {
            const y = padding.top + (chartH / 4) * i;
            ctx.strokeStyle = '#2A2A2A';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();

            const price = maxPrice - (priceRange / 4) * i;
            ctx.fillStyle = '#AAAAAA';
            ctx.font = '12px "Segoe UI", sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(price.toFixed(2), width - padding.right + 6, y);
        }

        // X-axis time labels
        ctx.textBaseline = 'alphabetic';
        const labelCount = Math.min(7, view.length);
        const step = Math.max(1, Math.floor(view.length / labelCount));
        ctx.fillStyle = '#B8B8B8';
        ctx.font = '12px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        let lastDay = null;
        for (let i = 0; i < view.length; i += step) {
            const t = view[i].time ? new Date(view[i].time) : null;
            if (!t) continue;
            const x = toX(i);
            const day = t.getDate();
            let label;
            if (timeframe === '1D') {
                label = t.toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });
            } else if (day !== lastDay) {
                label = t.toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });
                lastDay = day;
            } else {
                label = t.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: false });
            }
            ctx.fillText(label, x, height - 10);
        }

        const drawLine = (key, color, dash) => {
            ctx.strokeStyle = color;
            ctx.lineWidth = 1.25;
            ctx.setLineDash(dash || []);
            ctx.beginPath();
            let started = false;
            view.forEach((c, i) => {
                const v = num(c[key]);
                if (v == null || Number.isNaN(v)) { started = false; return; }
                const x = toX(i), y = toY(v);
                if (!started) { ctx.moveTo(x, y); started = true; }
                else ctx.lineTo(x, y);
            });
            ctx.stroke();
            ctx.setLineDash([]);
        };

        // Keltner Channels
        drawLine('keltnerUpperOuter', 'rgba(120,144,255,0.55)');
        drawLine('keltnerUpperInner', 'rgba(120,144,255,0.35)');
        drawLine('keltnerMid', 'rgba(120,144,255,0.45)', [4, 3]);
        drawLine('keltnerLowerInner', 'rgba(120,144,255,0.35)');
        drawLine('keltnerLowerOuter', 'rgba(120,144,255,0.55)');

        // VWAP
        drawLine('vwap', '#D4AF37', [2, 2]);

        // POC / VA / Camarilla horizontal levels
        drawLevels(ctx, st.levels, padding, width, chartH, toY);

        // Candles
        view.forEach((candle, i) => {
            const open = Number(candle.open);
            const close = Number(candle.close);
            const high = Number(candle.high);
            const low = Number(candle.low);
            const isUp = close >= open;
            const color = isUp ? '#00C853' : '#FF5252';

            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(toX(i), toY(high));
            ctx.lineTo(toX(i), toY(low));
            ctx.stroke();

            ctx.fillStyle = color;
            const bodyTop = Math.min(toY(open), toY(close));
            const bodyHeight = Math.max(1, Math.abs(toY(close) - toY(open)));
            ctx.fillRect(toX(i) - candleWidth / 2, bodyTop, candleWidth, bodyHeight);
        });

        // SuperTrend overlay — green above, red below
        ctx.lineWidth = 1.75;
        let segment = null;
        const flush = () => {
            if (!segment || segment.points.length < 2) { segment = null; return; }
            ctx.strokeStyle = segment.color;
            ctx.beginPath();
            segment.points.forEach((p, idx) => idx === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y));
            ctx.stroke();
            segment = null;
        };
        view.forEach((candle, i) => {
            const stv = candle.superTrend != null ? Number(candle.superTrend) : null;
            if (stv == null || Number.isNaN(stv)) { flush(); return; }
            const close = Number(candle.close);
            const color = close >= stv ? '#00C853' : '#FF5252';
            const point = { x: toX(i), y: toY(stv) };
            if (!segment || segment.color !== color) { flush(); segment = { color: color, points: [point] }; }
            else segment.points.push(point);
        });
        flush();
    }

    return {
        drawCandlestickChart: setData,
        zoom: zoom,
        resetZoom: resetZoom
    };
})();
