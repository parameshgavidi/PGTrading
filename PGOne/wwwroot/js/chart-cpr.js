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

    function drawLevelLabel(ctx, x, y, text, color) {
        if (!text) return;
        ctx.font = 'bold 10px "Segoe UI", sans-serif';
        var metrics = ctx.measureText(text);
        var padX = 4;
        var w = metrics.width + padX * 2;
        var h = 14;
        var top = y - h + 1;

        ctx.fillStyle = 'rgba(18, 18, 18, 0.82)';
        ctx.fillRect(x, top, w, h);

        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.strokeRect(x, top, w, h);

        ctx.fillStyle = color;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';
        ctx.fillText(text, x + padX, y);
    }

    function drawSegmentLevels(ctx, view, segments, padding, chartH, slot, toY, toX) {
        if (!segments || segments.length === 0 || !view.length) return;

        function segmentKey(seg) {
            if (!seg) return '';
            return String(seg.start) + '|' + String(seg.end) + '|' + String(seg.pivot);
        }

        var i = 0;
        while (i < view.length) {
            var seg = findSegment(segments, parseTime(view[i].time));
            if (!seg || !seg.pivot || Number.isNaN(Number(seg.pivot))) {
                i++;
                continue;
            }

            var key = segmentKey(seg);
            var iStart = i;
            i++;
            while (i < view.length) {
                var seg2 = findSegment(segments, parseTime(view[i].time));
                if (segmentKey(seg2) !== key) break;
                i++;
            }
            var iEnd = i - 1;

            var x0 = toX(iStart) - slot / 2;
            var x1 = toX(iEnd) + slot / 2;

            drawHLine(ctx, x0, x1, toY(Number(seg.tc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);
            drawHLine(ctx, x0, x1, toY(Number(seg.pivot)), 'rgba(255, 193, 7, 0.95)', [6, 4]);
            drawHLine(ctx, x0, x1, toY(Number(seg.bc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);

            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.tc)), 'TC', 'rgba(100, 181, 246, 0.95)');
            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.pivot)), 'CPR', 'rgba(255, 193, 7, 0.95)');
            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.bc)), 'BC', 'rgba(100, 181, 246, 0.95)');

            ctx.strokeStyle = 'rgba(0, 200, 83, 0.25)';
            ctx.lineWidth = 1;
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.moveTo(x0, padding.top);
            ctx.lineTo(x0, padding.top + chartH);
            ctx.stroke();
        }
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

    function drawKeltnerLines(ctx, view, toX, toY) {
        var keys = [
            { key: 'keltnerUpperOuter', color: 'rgba(120,144,255,0.55)', dash: [] },
            { key: 'keltnerUpperInner', color: 'rgba(120,144,255,0.35)', dash: [] },
            { key: 'keltnerMid', color: 'rgba(120,144,255,0.45)', dash: [4, 3] },
            { key: 'keltnerLowerInner', color: 'rgba(120,144,255,0.35)', dash: [] },
            { key: 'keltnerLowerOuter', color: 'rgba(120,144,255,0.55)', dash: [] }
        ];

        keys.forEach(function (line) {
            ctx.strokeStyle = line.color;
            ctx.lineWidth = 1.25;
            ctx.setLineDash(line.dash);
            ctx.beginPath();
            var started = false;
            view.forEach(function (candle, i) {
                var v = candle[line.key];
                if (v == null || Number.isNaN(Number(v))) {
                    started = false;
                    return;
                }
                var x = toX(i);
                var y = toY(Number(v));
                if (!started) {
                    ctx.moveTo(x, y);
                    started = true;
                } else {
                    ctx.lineTo(x, y);
                }
            });
            ctx.stroke();
            ctx.setLineDash([]);
        });
    }

    function setData(canvasId, candles, segments, overlayOptions) {
        var canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        var opts = overlayOptions || {};
        var showKeltner = opts.showKeltner === true;

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
            offset: offset,
            showKeltner: showKeltner
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
        var keltnerPrices = [];
        if (st.showKeltner) {
            view.forEach(function (c) {
                if (c.keltnerUpperOuter != null) keltnerPrices.push(Number(c.keltnerUpperOuter));
                if (c.keltnerLowerOuter != null) keltnerPrices.push(Number(c.keltnerLowerOuter));
            });
        }
        var maxCandidates = highs.concat(segPrices, keltnerPrices);
        var minCandidates = lows.concat(segPrices, keltnerPrices);
        var maxPrice = Math.max.apply(null, maxCandidates.length ? maxCandidates : [Number.MIN_VALUE]);
        var minPrice = Math.min.apply(null, minCandidates.length ? minCandidates : [Number.MAX_VALUE]);
        var priceRange = (maxPrice - minPrice) || 1;
        var slot = chartW / view.length;
        var candleWidth = Math.max(1, slot - 2);

        var toY = function (price) { return padding.top + ((maxPrice - price) / priceRange) * chartH; };
        var toX = function (i) { return padding.left + slot * i + slot / 2; };

        // Plain chart background (no green/red CPR shadow)
        ctx.fillStyle = '#121212';
        ctx.fillRect(padding.left, padding.top, chartW, chartH);

        if (st.showKeltner) {
            drawKeltnerLines(ctx, view, toX, toY);
        }

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
