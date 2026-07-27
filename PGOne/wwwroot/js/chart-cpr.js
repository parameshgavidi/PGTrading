// 1-minute chart with intraday CPR bands (15m pivot refresh) and above/below CPR background.
window.pgOneCprChart = (function () {
    const states = {};

    function num(v) { return v == null ? null : Number(v); }

    function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }

    function parseTime(value) {
        if (!value) return null;
        var t = new Date(value);
        return Number.isNaN(t.getTime()) ? null : t;
    }

    function findSegment(segments, time) {
        if (!segments || !time) return null;
        for (var i = 0; i < segments.length; i++) {
            var s = segments[i];
            var start = parseTime(s.start);
            var end = parseTime(s.end);
            if (!start || !end) continue;
            if (time >= start && time < end) return s;
        }
        return segments.length ? segments[segments.length - 1] : null;
    }

    function ensureInteractions(canvas, id) {
        if (canvas.dataset.pgCprBound === '1') return;
        canvas.dataset.pgCprBound = '1';

        canvas.addEventListener('wheel', function (e) {
            var st = states[id];
            if (!st) return;
            e.preventDefault();
            var factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
            zoomAt(id, factor);
        }, { passive: false });

        var dragging = false, lastX = 0;
        canvas.addEventListener('mousedown', function (e) { dragging = true; lastX = e.clientX; });
        window.addEventListener('mouseup', function () { dragging = false; });
        canvas.addEventListener('mousemove', function (e) {
            if (!dragging) return;
            var st = states[id];
            if (!st) return;
            var perBar = (canvas.clientWidth - 72) / st.count;
            var deltaBars = Math.round((e.clientX - lastX) / perBar);
            if (deltaBars !== 0) {
                st.offset = clamp(st.offset + deltaBars, 0, st.candles.length - st.count);
                lastX = e.clientX;
                render(id);
            }
        });
    }

    function zoomAt(id, factor) {
        var st = states[id];
        if (!st) return;
        var newCount = clamp(Math.round(st.count / factor), 20, st.candles.length);
        st.count = newCount;
        st.offset = clamp(st.offset, 0, st.candles.length - st.count);
        render(id);
    }

    function drawCprBackground(ctx, view, segments, padding, slot, chartH, toY, toX) {
        var leftBound = padding.left;
        var top = padding.top;
        var bottom = padding.top + chartH;

        view.forEach(function (candle, i) {
            var t = parseTime(candle.time);
            var seg = findSegment(segments, t);
            if (!seg || !seg.pivot) return;

            var pivotY = clamp(toY(Number(seg.pivot)), top, bottom);
            var x0 = toX(i) - slot / 2;
            var w = slot;

            ctx.fillStyle = 'rgba(0, 200, 83, 0.12)';
            ctx.fillRect(x0, top, w, pivotY - top);

            ctx.fillStyle = 'rgba(255, 82, 82, 0.12)';
            ctx.fillRect(x0, pivotY, w, bottom - pivotY);
        });
    }

    function drawSegmentLevels(ctx, view, segments, padding, chartH, slot, toY, toX) {
        if (!segments || segments.length === 0) return;

        segments.forEach(function (seg) {
            var start = parseTime(seg.start);
            var end = parseTime(seg.end);
            if (!start || !end) return;

            var iStart = -1, iEnd = -1;
            view.forEach(function (candle, i) {
                var t = parseTime(candle.time);
                if (!t) return;
                if (t >= start && t < end) {
                    if (iStart < 0) iStart = i;
                    iEnd = i;
                }
            });

            if (iStart < 0 || iEnd < 0) return;

            var x0 = toX(iStart) - slot / 2;
            var x1 = toX(iEnd) + slot / 2;

            drawHLine(ctx, x0, x1, toY(Number(seg.tc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);
            drawHLine(ctx, x0, x1, toY(Number(seg.pivot)), 'rgba(255, 193, 7, 0.95)', [6, 4]);
            drawHLine(ctx, x0, x1, toY(Number(seg.bc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);

            ctx.strokeStyle = 'rgba(0, 200, 83, 0.25)';
            ctx.lineWidth = 1;
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.moveTo(x0, padding.top);
            ctx.lineTo(x0, padding.top + chartH);
            ctx.stroke();
        });
    }

    function drawHLine(ctx, x0, x1, y, color, dash) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.25;
        ctx.setLineDash(dash || []);
        ctx.beginPath();
        ctx.moveTo(x0, y);
        ctx.lineTo(x1, y);
        ctx.stroke();
        ctx.setLineDash([]);
    }

    function setData(canvasId, candles, segments) {
        var canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        var prev = states[canvasId];
        var count, offset;
        if (prev) {
            count = clamp(prev.count, 20, candles.length);
            offset = clamp(prev.offset, 0, candles.length - count);
        } else {
            count = candles.length;
            offset = 0;
        }

        states[canvasId] = {
            canvas: canvas,
            candles: candles,
            segments: segments || [],
            count: count,
            offset: offset
        };
        ensureInteractions(canvas, canvasId);
        render(canvasId);
    }

    function zoom(canvasId, factor) { zoomAt(canvasId, factor); }

    function resetZoom(canvasId) {
        var st = states[canvasId];
        if (!st) return;
        st.count = st.candles.length;
        st.offset = 0;
        render(canvasId);
    }

    function render(id) {
        var st = states[id];
        if (!st) return;
        var canvas = st.canvas;
        var candles = st.candles;
        var segments = st.segments;

        var container = canvas.parentElement;
        var cssW = Math.max((container ? container.clientWidth : canvas.clientWidth) - 4, 320);
        var cssH = Math.max((container ? container.clientHeight : canvas.clientHeight) - 4, 240);

        var dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
        canvas.style.width = cssW + 'px';
        canvas.style.height = cssH + 'px';

        var ctx = canvas.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        var width = cssW;
        var height = cssH;
        var padding = { top: 10, right: 52, bottom: 22, left: 6 };
        var chartW = width - padding.left - padding.right;
        var chartH = height - padding.top - padding.bottom;

        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = '#121212';
        ctx.fillRect(0, 0, width, height);

        var start = clamp(candles.length - st.offset - st.count, 0, Math.max(0, candles.length - 1));
        var end = clamp(candles.length - st.offset, 1, candles.length);
        var view = candles.slice(start, end);
        if (view.length === 0) return;

        var highs = view.map(function (c) { return Number(c.high); });
        var lows = view.map(function (c) { return Number(c.low); });
        var segPrices = [];
        segments.forEach(function (s) {
            segPrices.push(Number(s.tc), Number(s.pivot), Number(s.bc));
        });
        var maxPrice = Math.max.apply(null, highs.concat(segPrices.length ? segPrices : [Number.MIN_VALUE]));
        var minPrice = Math.min.apply(null, lows.concat(segPrices.length ? segPrices : [Number.MAX_VALUE]));
        var priceRange = (maxPrice - minPrice) || 1;
        var slot = chartW / view.length;
        var candleWidth = Math.max(1, slot - 2);

        var toY = function (price) { return padding.top + ((maxPrice - price) / priceRange) * chartH; };
        var toX = function (i) { return padding.left + slot * i + slot / 2; };

        drawCprBackground(ctx, view, segments, padding, slot, chartH, toY, toX);

        ctx.textBaseline = 'middle';
        for (var g = 0; g <= 4; g++) {
            var gy = padding.top + (chartH / 4) * g;
            ctx.strokeStyle = '#2A2A2A';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(padding.left, gy);
            ctx.lineTo(width - padding.right, gy);
            ctx.stroke();

            var price = maxPrice - (priceRange / 4) * g;
            ctx.fillStyle = '#AAAAAA';
            ctx.font = '12px "Segoe UI", sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(price.toFixed(2), width - padding.right + 6, gy);
        }

            drawSegmentLevels(ctx, view, segments, padding, chartH, slot, toY, toX);

        view.forEach(function (candle, i) {
            var open = Number(candle.open);
            var close = Number(candle.close);
            var high = Number(candle.high);
            var low = Number(candle.low);
            var isUp = close >= open;
            var color = isUp ? '#00C853' : '#FF5252';

            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(toX(i), toY(high));
            ctx.lineTo(toX(i), toY(low));
            ctx.stroke();

            ctx.fillStyle = color;
            var bodyTop = Math.min(toY(open), toY(close));
            var bodyHeight = Math.max(1, Math.abs(toY(close) - toY(open)));
            ctx.fillRect(toX(i) - candleWidth / 2, bodyTop, candleWidth, bodyHeight);
        });

        var labelCount = Math.min(8, view.length);
        var step = Math.max(1, Math.floor(view.length / labelCount));
        ctx.fillStyle = '#B8B8B8';
        ctx.font = '12px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'alphabetic';
        for (var li = 0; li < view.length; li += step) {
            var lt = parseTime(view[li].time);
            if (!lt) continue;
            var label = lt.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: false });
            ctx.fillText(label, toX(li), height - 10);
        }
    }

    return {
        draw: setData,
        zoom: zoom,
        resetZoom: resetZoom
    };
})();
