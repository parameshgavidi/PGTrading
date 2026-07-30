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
        if (g === 'cpr') {
            if (kind === 'sell') return 'rgba(255, 193, 7, 0.75)';
            if (kind === 'buy') return 'rgba(100, 181, 246, 0.75)';
            return 'rgba(212, 175, 55, 0.85)';
        }
        return 'rgba(200, 200, 200, 0.55)';
    }

    function parseTime(value) {
        if (!value) return null;
        var t = new Date(value);
        return Number.isNaN(t.getTime()) ? null : t;
    }

    function findCprSegment(segments, time) {
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

    function fillChartBackground(ctx, padding, width, chartH) {
        var left = padding.left;
        var w = width - padding.right - left;
        ctx.fillStyle = '#121212';
        ctx.fillRect(left, padding.top, w, chartH);
    }

    function drawPocBackground(ctx, pocToday, padding, width, chartH, toY, showPoc) {
        var left = padding.left;
        var right = width - padding.right;
        var top = padding.top;
        var bottom = padding.top + chartH;
        var w = right - left;

        if (!showPoc || !pocToday || Number.isNaN(pocToday)) {
            fillChartBackground(ctx, padding, width, chartH);
            return;
        }

        var y = clamp(toY(pocToday), top, bottom);

        ctx.fillStyle = 'rgba(0, 200, 83, 0.1)';
        ctx.fillRect(left, top, w, y - top);

        ctx.fillStyle = 'rgba(255, 82, 82, 0.1)';
        ctx.fillRect(left, y, w, bottom - y);
    }

    function drawIntradaCprBackground(ctx, view, segments, padding, slot, chartH, toY, toX) {
        var top = padding.top;
        var bottom = padding.top + chartH;

        view.forEach(function (candle, i) {
            var t = parseTime(candle.time);
            var seg = findCprSegment(segments, t);
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

    function drawHLineSegment(ctx, x0, x1, y, color, dash) {
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.25;
        ctx.setLineDash(dash || []);
        ctx.beginPath();
        ctx.moveTo(x0, y);
        ctx.lineTo(x1, y);
        ctx.stroke();
        ctx.setLineDash([]);
    }

    function intradayCprSegmentKey(seg) {
        if (!seg) return '';
        return String(seg.start) + '|' + String(seg.end);
    }

    function drawIntradaCprLevels(ctx, view, segments, padding, chartH, slot, toY, toX) {
        if (!segments || segments.length === 0 || !view.length) return;

        var i = 0;
        while (i < view.length) {
            var t = parseTime(view[i].time);
            var seg = findCprSegment(segments, t);
            if (!seg || !seg.pivot) {
                i++;
                continue;
            }

            var key = intradayCprSegmentKey(seg);
            var iStart = i;
            i++;
            while (i < view.length) {
                var t2 = parseTime(view[i].time);
                var seg2 = findCprSegment(segments, t2);
                if (intradayCprSegmentKey(seg2) !== key) break;
                i++;
            }
            var iEnd = i - 1;

            var x0 = toX(iStart) - slot / 2;
            var x1 = toX(iEnd) + slot / 2;

            drawHLineSegment(ctx, x0, x1, toY(Number(seg.tc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);
            drawHLineSegment(ctx, x0, x1, toY(Number(seg.pivot)), 'rgba(255, 193, 7, 0.95)', [6, 4]);
            drawHLineSegment(ctx, x0, x1, toY(Number(seg.bc)), 'rgba(100, 181, 246, 0.9)', [4, 4]);

            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.tc)), 'TC', 'rgba(100, 181, 246, 0.95)');
            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.pivot)), 'CPR', 'rgba(255, 193, 7, 0.95)');
            drawLevelLabel(ctx, x0 + 2, toY(Number(seg.bc)), 'BC', 'rgba(100, 181, 246, 0.95)');

            ctx.strokeStyle = 'rgba(0, 200, 83, 0.25)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x0, padding.top);
            ctx.lineTo(x0, padding.top + chartH);
            ctx.stroke();
        }
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

    function drawLevels(ctx, levels, padding, width, toY) {
        if (!levels || levels.length === 0) return;

        levels.forEach(function (lv) {
            const price = Number(lv.price);
            if (!price || Number.isNaN(price)) return;

            const y = toY(price);
            const color = levelColor(lv);
            const dash = lv.group === 'prev' ? [5, 4] : [8, 4];
            const isPocToday = (lv.label || '').toLowerCase().startsWith('poc today');

            ctx.strokeStyle = color;
            ctx.lineWidth = isPocToday ? 1.75 : lv.group === 'today' ? 1.35 : 1;
            ctx.setLineDash(dash);
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();
            ctx.setLineDash([]);

            drawLevelLabel(ctx, padding.left + 2, y, lv.label || '', color);
        });
    }

    function filterLevels(st) {
        return (st.levels || []).filter(function (lv) {
            if (lv.group === 'cam' && !st.showCamarilla) return false;
            if (lv.group === 'cpr' && !st.showPivot) return false;
            if ((lv.group === 'today' || lv.group === 'prev') && !st.showPoc) return false;
            return true;
        });
    }

    function setData(canvasId, candles, timeframe, levels, pocToday, overlayOptions) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !candles || candles.length === 0) return;

        const prev = states[canvasId];
        const opts = overlayOptions || {};
        const showPoc = opts.showPoc !== false;
        const showPivot = opts.showPivot !== false;
        const showCamarilla = opts.showCamarilla !== false;
        const showIntradaCpr = opts.showIntradaCpr === true;
        const intradayCprSegments = opts.intradayCpr || [];
        const showKeltner = opts.showKeltner === true;
        const showVwap = opts.showVwap === true;
        const showSuperTrend = opts.showSuperTrend === true;
        const showEma20 = opts.showEma20 === true;
        let count, offset;
        if (prev && prev.timeframe === timeframe) {
            count = clamp(prev.count, 12, candles.length);
            offset = clamp(prev.offset, 0, candles.length - count);
        } else {
            count = candles.length; // show everything by default
            offset = 0;
        }

        states[canvasId] = {
            canvas, candles, timeframe, count, offset,
            levels: levels || [],
            pocToday: num(pocToday),
            showPoc: showPoc,
            showPivot: showPivot,
            showCamarilla: showCamarilla,
            showIntradaCpr: showIntradaCpr,
            intradayCprSegments: intradayCprSegments,
            showKeltner: showKeltner,
            showVwap: showVwap,
            showSuperTrend: showSuperTrend,
            showEma20: showEma20
        };
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
        const levelPrices = filterLevels(st).map(l => Number(l.price)).filter(v => !Number.isNaN(v));
        var intradayPrices = [];
        if (st.showIntradaCpr && st.intradayCprSegments) {
            st.intradayCprSegments.forEach(function (s) {
                intradayPrices.push(Number(s.tc), Number(s.pivot), Number(s.bc));
            });
        }
        const extra = [];
        if (st.showSuperTrend) {
            extra.push.apply(extra, collect('superTrend'));
        }
        if (st.showKeltner) {
            extra.push.apply(extra, collect('keltnerUpperOuter'));
            extra.push.apply(extra, collect('keltnerLowerOuter'));
        }
        if (st.showVwap) {
            extra.push.apply(extra, collect('vwap'));
        }
        if (st.showEma20) {
            extra.push.apply(extra, collect('ema20'));
        }
        const maxPrice = Math.max(...highs, ...(extra.length ? extra : [Number.MIN_VALUE]), ...(levelPrices.length ? levelPrices : [Number.MIN_VALUE]), ...(intradayPrices.length ? intradayPrices : [Number.MIN_VALUE]));
        const minPrice = Math.min(...lows, ...(extra.length ? extra : [Number.MAX_VALUE]), ...(levelPrices.length ? levelPrices : [Number.MAX_VALUE]), ...(intradayPrices.length ? intradayPrices : [Number.MAX_VALUE]));
        const priceRange = (maxPrice - minPrice) || 1;
        const slot = chartW / view.length;
        const candleWidth = Math.max(1.5, slot - 2);

        const toY = (price) => padding.top + ((maxPrice - price) / priceRange) * chartH;
        const toX = (i) => padding.left + slot * i + slot / 2;

        if (st.showIntradaCpr && st.intradayCprSegments && st.intradayCprSegments.length > 0) {
            drawIntradaCprBackground(ctx, view, st.intradayCprSegments, padding, slot, chartH, toY, toX);
        } else if (st.showPoc && st.pocToday) {
            drawPocBackground(ctx, st.pocToday, padding, width, chartH, toY, true);
        } else {
            fillChartBackground(ctx, padding, width, chartH);
        }

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

        // Keltner Channels (1m / 5m)
        if (st.showKeltner) {
            drawLine('keltnerUpperOuter', 'rgba(120,144,255,0.55)');
            drawLine('keltnerUpperInner', 'rgba(120,144,255,0.35)');
            drawLine('keltnerMid', 'rgba(120,144,255,0.45)', [4, 3]);
            drawLine('keltnerLowerInner', 'rgba(120,144,255,0.35)');
            drawLine('keltnerLowerOuter', 'rgba(120,144,255,0.55)');
        }

        // VWAP (5m toggle)
        if (st.showVwap) {
            drawLine('vwap', '#D4AF37', [2, 2]);
        }

        // EMA 20 (5m toggle)
        if (st.showEma20) {
            drawLine('ema20', 'rgba(255, 152, 0, 0.9)', [6, 3]);
        }

        // POC / VA / Camarilla horizontal levels
        drawLevels(ctx, filterLevels(st), padding, width, toY);

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

        if (st.showIntradaCpr && st.intradayCprSegments && st.intradayCprSegments.length > 0) {
            drawIntradaCprLevels(ctx, view, st.intradayCprSegments, padding, chartH, slot, toY, toX);
        }

        // SuperTrend overlay (10,3) — green above, red below
        if (st.showSuperTrend) {
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
    }

    return {
        drawCandlestickChart: setData,
        zoom: zoom,
        resetZoom: resetZoom
    };
})();
