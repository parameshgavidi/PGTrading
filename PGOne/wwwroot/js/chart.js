// Candlestick chart renderer for PG One
window.pgOneChart = {
    drawCandlestickChart: function (canvasId, candles) {
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
        const padding = { top: 20, right: 60, bottom: 30, left: 10 };
        const chartW = width - padding.left - padding.right;
        const chartH = height - padding.top - padding.bottom;

        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = '#0A0A0A';
        ctx.fillRect(0, 0, width, height);

        const highs = candles.map(c => Number(c.high));
        const lows = candles.map(c => Number(c.low));
        const stValues = candles.map(c => c.superTrend != null ? Number(c.superTrend) : null).filter(v => v != null && !Number.isNaN(v));
        const maxPrice = Math.max(...highs, ...(stValues.length ? stValues : [0]));
        const minPrice = Math.min(...lows, ...(stValues.length ? stValues : [Number.MAX_VALUE]));
        const priceRange = maxPrice - minPrice || 1;
        const candleWidth = Math.max(2, chartW / candles.length - 2);

        const toY = (price) => padding.top + ((maxPrice - price) / priceRange) * chartH;
        const toX = (i) => padding.left + (chartW / candles.length) * i + candleWidth / 2;

        // Grid lines
        ctx.strokeStyle = '#1A1A1A';
        ctx.lineWidth = 1;
        for (let i = 0; i <= 4; i++) {
            const y = padding.top + (chartH / 4) * i;
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();

            const price = maxPrice - (priceRange / 4) * i;
            ctx.fillStyle = '#666';
            ctx.font = '10px sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(price.toFixed(0), width - padding.right + 4, y + 4);
        }

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

        // SuperTrend overlay — color by close vs SuperTrend
        ctx.lineWidth = 1.5;
        let segment = null;
        const flush = () => {
            if (!segment || segment.points.length < 2) return;
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
