// Candlestick chart renderer for PG One
window.pgOneChart = {
    drawCandlestickChart: function (canvasId, candles, timeframe) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        const container = canvas.parentElement;
        if (container) {
            const width = Math.max(container.clientWidth - 8, 320);
            const height = Math.max(container.clientHeight - 8, 240);
            canvas.width = width;
            canvas.height = height;
            canvas.style.width = width + 'px';
            canvas.style.height = height + 'px';
        }

        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        const padding = { top: 20, right: 62, bottom: 26, left: 10 };
        const chartW = width - padding.left - padding.right;
        const chartH = height - padding.top - padding.bottom;

        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = '#0A0A0A';
        ctx.fillRect(0, 0, width, height);

        const num = (v) => (v == null ? null : Number(v));
        const collect = (key) => candles.map(c => num(c[key])).filter(v => v != null && !Number.isNaN(v));

        const highs = candles.map(c => Number(c.high));
        const lows = candles.map(c => Number(c.low));
        // Include overlays in the price scaling so nothing is clipped.
        const extra = [].concat(collect('superTrend'), collect('keltnerUpperOuter'), collect('keltnerLowerOuter'), collect('vwap'));
        const maxPrice = Math.max(...highs, ...(extra.length ? extra : [Number.MIN_VALUE]));
        const minPrice = Math.min(...lows, ...(extra.length ? extra : [Number.MAX_VALUE]));
        const priceRange = maxPrice - minPrice || 1;
        const candleWidth = Math.max(2, chartW / candles.length - 2);

        const toY = (price) => padding.top + ((maxPrice - price) / priceRange) * chartH;
        const toX = (i) => padding.left + (chartW / candles.length) * i + candleWidth / 2;

        // Horizontal grid + price labels
        ctx.strokeStyle = '#1A1A1A';
        ctx.lineWidth = 1;
        for (let i = 0; i <= 4; i++) {
            const y = padding.top + (chartH / 4) * i;
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();

            const price = maxPrice - (priceRange / 4) * i;
            ctx.fillStyle = '#888';
            ctx.font = '10px sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(price.toFixed(2), width - padding.right + 4, y + 4);
        }

        // X-axis time labels
        const labelCount = Math.min(6, candles.length);
        const step = Math.max(1, Math.floor(candles.length / labelCount));
        ctx.fillStyle = '#888';
        ctx.font = '10px sans-serif';
        ctx.textAlign = 'center';
        let lastDay = null;
        for (let i = 0; i < candles.length; i += step) {
            const t = candles[i].time ? new Date(candles[i].time) : null;
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
            ctx.fillText(label, x, height - 8);
        }

        // Helper: draw a continuous line for a value accessor
        const drawLine = (key, color, dash) => {
            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.setLineDash(dash || []);
            ctx.beginPath();
            let started = false;
            candles.forEach((c, i) => {
                const v = num(c[key]);
                if (v == null || Number.isNaN(v)) { started = false; return; }
                const x = toX(i), y = toY(v);
                if (!started) { ctx.moveTo(x, y); started = true; }
                else ctx.lineTo(x, y);
            });
            ctx.stroke();
            ctx.setLineDash([]);
        };

        // Keltner Channels (drawn behind candles)
        drawLine('keltnerUpperOuter', 'rgba(120,144,255,0.55)');
        drawLine('keltnerUpperInner', 'rgba(120,144,255,0.35)');
        drawLine('keltnerMid', 'rgba(120,144,255,0.45)', [4, 3]);
        drawLine('keltnerLowerInner', 'rgba(120,144,255,0.35)');
        drawLine('keltnerLowerOuter', 'rgba(120,144,255,0.55)');

        // VWAP (yellow)
        drawLine('vwap', '#D4AF37', [2, 2]);

        // Candles
        candles.forEach((candle, i) => {
            const open = Number(candle.open);
            const close = Number(candle.close);
            const high = Number(candle.high);
            const low = Number(candle.low);
            const isUp = close >= open;
            const color = isUp ? '#00C853' : '#FF1744';

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

        // SuperTrend overlay — green when price above ST, red when below
        ctx.lineWidth = 1.5;
        let segment = null;
        const flush = () => {
            if (!segment || segment.points.length < 2) { segment = null; return; }
            ctx.strokeStyle = segment.color;
            ctx.beginPath();
            segment.points.forEach((p, idx) => {
                if (idx === 0) ctx.moveTo(p.x, p.y);
                else ctx.lineTo(p.x, p.y);
            });
            ctx.stroke();
            segment = null;
        };

        candles.forEach((candle, i) => {
            const st = candle.superTrend != null ? Number(candle.superTrend) : null;
            if (st == null || Number.isNaN(st)) {
                flush();
                return;
            }

            const close = Number(candle.close);
            const color = close >= st ? '#00C853' : '#FF1744';
            const point = { x: toX(i), y: toY(st) };

            if (!segment || segment.color !== color) {
                flush();
                segment = { color: color, points: [point] };
            } else {
                segment.points.push(point);
            }
        });
        flush();
    }
};
