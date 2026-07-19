// Simple candlestick chart renderer for PG One
window.pgOneChart = {
    drawCandlestickChart: function (canvasId, candles) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        const padding = { top: 20, right: 60, bottom: 30, left: 10 };
        const chartW = width - padding.left - padding.right;
        const chartH = height - padding.top - padding.bottom;

        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = '#0A0A0A';
        ctx.fillRect(0, 0, width, height);

        const highs = candles.map(c => c.high);
        const lows = candles.map(c => c.low);
        const maxPrice = Math.max(...highs);
        const minPrice = Math.min(...lows);
        const priceRange = maxPrice - minPrice || 1;
        const candleWidth = Math.max(2, chartW / candles.length - 2);

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
            const x = padding.left + (chartW / candles.length) * i + candleWidth / 2;
            const isUp = candle.close >= candle.open;
            const color = isUp ? '#00C853' : '#FF1744';

            const yHigh = padding.top + ((maxPrice - candle.high) / priceRange) * chartH;
            const yLow = padding.top + ((maxPrice - candle.low) / priceRange) * chartH;
            const yOpen = padding.top + ((maxPrice - candle.open) / priceRange) * chartH;
            const yClose = padding.top + ((maxPrice - candle.close) / priceRange) * chartH;

            // Wick
            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x, yHigh);
            ctx.lineTo(x, yLow);
            ctx.stroke();

            // Body
            ctx.fillStyle = color;
            const bodyTop = Math.min(yOpen, yClose);
            const bodyHeight = Math.max(1, Math.abs(yClose - yOpen));
            ctx.fillRect(x - candleWidth / 2, bodyTop, candleWidth, bodyHeight);
        });

        // SuperTrend line (simplified overlay)
        ctx.strokeStyle = '#2196F3';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        candles.forEach((candle, i) => {
            const x = padding.left + (chartW / candles.length) * i + candleWidth / 2;
            const st = candle.low * 0.998;
            const y = padding.top + ((maxPrice - st) / priceRange) * chartH;
            if (i === 0) ctx.moveTo(x, y);
            else ctx.lineTo(x, y);
        });
        ctx.stroke();
    }
};

document.addEventListener('DOMContentLoaded', function () {
    // Auto-draw demo chart if canvas exists
    setTimeout(function () {
        const demoCandles = [];
        let price = 25300;
        for (let i = 0; i < 50; i++) {
            const change = (Math.random() - 0.48) * 30;
            const open = price;
            const close = price + change;
            demoCandles.push({
                open: open,
                high: Math.max(open, close) + Math.random() * 10,
                low: Math.min(open, close) - Math.random() * 10,
                close: close
            });
            price = close;
        }
        window.pgOneChart.drawCandlestickChart('priceChart', demoCandles);
        window.pgOneChart.drawCandlestickChart('liveChart', demoCandles);
    }, 500);
});
