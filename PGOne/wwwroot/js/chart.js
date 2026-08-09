// Candlestick chart renderer for PG One with zoom/pan support.
window.pgOneChart = (function () {
    const states = {};
    const PRICE_AXIS_WIDTH = 56;
    const Y_SCALE_MIN = 0.05;
    const Y_SCALE_MAX = 20;
    const CHART_PADDING = { top: 10, right: 52, bottom: 22, left: 6 };

    function num(v) { return v == null ? null : Number(v); }

  // Blazor interop may send camelCase or PascalCase property names.
    function field(obj, key) {
        if (!obj) return null;
        var v = obj[key];
        if (v != null) return v;
        var alt = key.charAt(0).toUpperCase() + key.slice(1);
        return alt !== key ? obj[alt] : null;
    }

    function overlayFlag(opts, camel, fallback) {
        if (!opts) return fallback;
        var keys = [camel, camel.charAt(0).toUpperCase() + camel.slice(1)];
        for (var i = 0; i < keys.length; i++) {
            var v = opts[keys[i]];
            if (v === true || v === 1 || v === '1' || v === 'true') return true;
            if (v === false || v === 0 || v === '0' || v === 'false') return false;
        }
        return fallback;
    }

    function normalizeCandle(raw) {
        if (!raw) return raw;
        var entry = num(field(raw, 'superTrendEntry'));
        var st725Val = num(field(raw, 'st725'));
        var st725Line = num(field(raw, 'superTrend725'));
        var st725 = st725Line != null ? st725Line : (st725Val != null ? st725Val : entry);
        if (entry == null && st725 != null) entry = st725;
        return {
            time: field(raw, 'time'),
            open: num(field(raw, 'open')),
            high: num(field(raw, 'high')),
            low: num(field(raw, 'low')),
            close: num(field(raw, 'close')),
            superTrend: num(field(raw, 'superTrend')),
            superTrend725: st725,
            superTrendEntry: entry,
            st725: st725,
            keltnerUpperInner: num(field(raw, 'keltnerUpperInner')),
            keltnerLowerInner: num(field(raw, 'keltnerLowerInner')),
            keltnerUpperOuter: num(field(raw, 'keltnerUpperOuter')),
            keltnerLowerOuter: num(field(raw, 'keltnerLowerOuter')),
            keltnerMid: num(field(raw, 'keltnerMid')),
            vwap: getNormalizedStudy(field(raw, 'vwap'), field(raw, 'vwapLine')),
            vwapLine: getNormalizedStudy(field(raw, 'vwapLine'), field(raw, 'vwap')),
            ema9: getNormalizedStudy(field(raw, 'ema9'), field(raw, 'ema9Line')),
            ema9Line: getNormalizedStudy(field(raw, 'ema9Line'), field(raw, 'ema9')),
            ema20: getNormalizedStudy(field(raw, 'ema20'), field(raw, 'ema20Line')),
            ema20Line: getNormalizedStudy(field(raw, 'ema20Line'), field(raw, 'ema20')),
            ema50: getNormalizedStudy(field(raw, 'ema50'), field(raw, 'ema50Line')),
            ema50Line: getNormalizedStudy(field(raw, 'ema50Line'), field(raw, 'ema50')),
            ema200: getNormalizedStudy(field(raw, 'ema200'), field(raw, 'ema200Line')),
            ema200Line: getNormalizedStudy(field(raw, 'ema200Line'), field(raw, 'ema200')),
            volume: num(field(raw, 'volume')),
            patternCode: field(raw, 'patternCode') || null,
            patternLabel: field(raw, 'patternLabel') || null,
            patternBias: field(raw, 'patternBias') || null
        };
    }

    function getNormalizedStudy(primary, fallback) {
        var v = num(primary);
        if (v != null) return v;
        return num(fallback);
    }

    function getVwapValue(candle) {
        if (!candle) return null;
        var v = candle.vwapLine;
        if (v != null && !Number.isNaN(v)) return v;
        v = candle.vwap;
        return v != null && !Number.isNaN(v) ? v : null;
    }

    function getEmaValue(candle, key) {
        if (!candle) return null;
        var lineKey = key + 'Line';
        var v = candle[lineKey];
        if (v != null && !Number.isNaN(v)) return v;
        v = candle[key];
        return v != null && !Number.isNaN(v) ? v : null;
    }

    function getEma9Value(candle) { return getEmaValue(candle, 'ema9'); }
    function getEma20Value(candle) { return getEmaValue(candle, 'ema20'); }
    function getEma50Value(candle) { return getEmaValue(candle, 'ema50'); }
    function getEma200Value(candle) { return getEmaValue(candle, 'ema200'); }

    function assignVwapValue(candle, value) {
        candle.vwap = value;
        candle.vwapLine = value;
    }

    function assignEmaValue(candle, key, value) {
        candle[key] = value;
        candle[key + 'Line'] = value;
    }

    function assignEma20Value(candle, value) {
        assignEmaValue(candle, 'ema20', value);
    }

    function getSt725Value(candle) {
        if (!candle) return null;
        var v = candle.superTrend725;
        if (v != null && !Number.isNaN(v)) return v;
        v = candle.st725;
        if (v != null && !Number.isNaN(v)) return v;
        v = candle.superTrendEntry;
        return v != null && !Number.isNaN(v) ? v : null;
    }

    function assignSt725Values(candle, value) {
        candle.superTrend725 = value;
        candle.st725 = value;
        candle.superTrendEntry = value;
    }

    function studyFieldMissing(candles, key, minCount) {
        var found = 0;
        for (var i = 0; i < candles.length; i++) {
            var v = candles[i][key];
            if (v != null && !Number.isNaN(v)) found++;
        }
        return found < (minCount || Math.min(3, Math.floor(candles.length / 4)));
    }

    function computeEma(candles, period, key) {
        period = period || 20;
        key = key || ('ema' + period);
        if (candles.length < period) return;
        var k = 2 / (period + 1);
        var sum = 0;
        for (var i = 0; i < period; i++) sum += candles[i].close;
        var ema = sum / period;
        assignEmaValue(candles[period - 1], key, ema);
        for (var j = period; j < candles.length; j++) {
            ema = candles[j].close * k + ema * (1 - k);
            assignEmaValue(candles[j], key, ema);
        }
    }

    function computeEma20(candles, period) {
        computeEma(candles, period || 20, 'ema20');
    }

    function computeSessionVwap(candles) {
        var dayKey = null, cumPv = 0, cumVol = 0, cumTyp = 0, cnt = 0;
        for (var i = 0; i < candles.length; i++) {
            var c = candles[i];
            var t = c.time ? new Date(c.time) : null;
            var dk = t ? (t.getFullYear() + '-' + t.getMonth() + '-' + t.getDate()) : String(i);
            if (dayKey !== dk) {
                dayKey = dk;
                cumPv = 0;
                cumVol = 0;
                cumTyp = 0;
                cnt = 0;
            }
            var typical = (c.high + c.low + c.close) / 3;
            var vol = c.volume || 0;
            cumPv += typical * vol;
            cumVol += vol;
            cumTyp += typical;
            cnt++;
            assignVwapValue(c, cumVol > 0 ? cumPv / cumVol : cumTyp / cnt);
        }
    }

    function computeSuperTrendSeries(candles, period, multiplier, key) {
        var n = candles.length;
        if (n < period + 1) return;
        var atr = new Array(n).fill(0);
        var tr = new Array(n);
        tr[0] = candles[0].high - candles[0].low;
        for (var i = 1; i < n; i++) {
            var hl = candles[i].high - candles[i].low;
            var hc = Math.abs(candles[i].high - candles[i - 1].close);
            var lc = Math.abs(candles[i].low - candles[i - 1].close);
            tr[i] = Math.max(hl, hc, lc);
        }
        var sum = 0;
        for (var p = 0; p < period; p++) sum += tr[p];
        atr[period - 1] = sum / period;
        for (var a = period; a < n; a++) {
            atr[a] = (atr[a - 1] * (period - 1) + tr[a]) / period;
        }
        var prevUpper = 0, prevLower = 0, prevSt = 0, started = false;
        for (var i = 0; i < n; i++) {
            if (atr[i] <= 0) continue;
            var hl2 = (candles[i].high + candles[i].low) / 2;
            var upper = hl2 + multiplier * atr[i];
            var lower = hl2 - multiplier * atr[i];
            if (!started) {
                prevUpper = upper;
                prevLower = lower;
                prevSt = upper;
                if (key === 'superTrend725' || key === 'superTrendEntry') assignSt725Values(candles[i], prevSt);
                else candles[i][key] = prevSt;
                started = true;
                continue;
            }
            var prevClose = candles[i - 1].close;
            lower = (lower > prevLower || prevClose < prevLower) ? lower : prevLower;
            upper = (upper < prevUpper || prevClose > prevUpper) ? upper : prevUpper;
            var wasDown = prevSt === prevUpper;
            var isUp = wasDown ? candles[i].close > upper : !(candles[i].close < lower);
            var st = isUp ? lower : upper;
            if (key === 'superTrend725' || key === 'superTrendEntry') assignSt725Values(candles[i], st);
            else candles[i][key] = st;
            prevUpper = upper;
            prevLower = lower;
            prevSt = st;
        }
    }

    function ensureStudyIndicators(candles, flags) {
        if (!candles || candles.length === 0) return;
        // Always compute when overlay is enabled so toggling ON never hits empty series.
        if (flags.showEma9) computeEma(candles, 9, 'ema9');
        if (flags.showEma20) computeEma(candles, 20, 'ema20');
        if (flags.showEma50) computeEma(candles, 50, 'ema50');
        if (flags.showEma200) computeEma(candles, 200, 'ema200');
        if (flags.showVwap) computeSessionVwap(candles);
        if (flags.showSuperTrend725) computeSuperTrendSeries(candles, 7, 2.5, 'superTrend725');
        if (flags.showSuperTrend) computeSuperTrendSeries(candles, 10, 3, 'superTrend');
    }

    function studyFlagsFromState(st) {
        return {
            showVwap: !!st.showVwap,
            showEma9: !!st.showEma9,
            showEma20: !!st.showEma20,
            showEma50: !!st.showEma50,
            showEma200: !!st.showEma200,
            showSuperTrend725: !!st.showSuperTrend725,
            showSuperTrend: !!st.showSuperTrend
        };
    }

    function intOverlayFlag(v) {
        return v === true || v === 1 || v === '1' || v === 'true';
    }

    function mergeOverlayInts(opts, ints) {
        if (!ints) return opts || {};
        var merged = Object.assign({}, opts || {});
        if (ints.showVwap != null) merged.showVwap = intOverlayFlag(ints.showVwap) ? 1 : 0;
        if (ints.showEma9 != null) merged.showEma9 = intOverlayFlag(ints.showEma9) ? 1 : 0;
        if (ints.showEma20 != null) merged.showEma20 = intOverlayFlag(ints.showEma20) ? 1 : 0;
        if (ints.showEma50 != null) merged.showEma50 = intOverlayFlag(ints.showEma50) ? 1 : 0;
        if (ints.showEma200 != null) merged.showEma200 = intOverlayFlag(ints.showEma200) ? 1 : 0;
        if (ints.showPatterns != null) merged.showPatterns = intOverlayFlag(ints.showPatterns) ? 1 : 0;
        if (ints.showSuperTrend725 != null) merged.showSuperTrend725 = intOverlayFlag(ints.showSuperTrend725) ? 1 : 0;
        if (ints.showSuperTrend != null) merged.showSuperTrend = intOverlayFlag(ints.showSuperTrend) ? 1 : 0;
        return merged;
    }

    function isPriceAxisEvent(canvas, e) {
        const rect = canvas.getBoundingClientRect();
        return e.clientX - rect.left >= rect.width - PRICE_AXIS_WIDTH;
    }

    function getChartView(st) {
        const candles = st.candles;
        const start = clamp(candles.length - st.offset - st.count, 0, Math.max(0, candles.length - 1));
        const end = clamp(candles.length - st.offset, 1, candles.length);
        return candles.slice(start, end);
    }

    function collectViewExtents(st, view) {
        const collect = (key) => view.map(c => c[key]).filter(v => v != null && !Number.isNaN(v));
        const highs = view.map(c => c.high);
        const lows = view.map(c => c.low);
        const levelPrices = filterLevels(st).map(l => Number(l.price)).filter(v => !Number.isNaN(v));
        var intradayPrices = [];
        if (st.showIntradaCpr && st.intradayCprSegments) {
            st.intradayCprSegments.forEach(function (s) {
                intradayPrices.push(Number(s.tc), Number(s.pivot), Number(s.bc));
            });
        }
        const extra = [];
        if (st.showSuperTrend) extra.push.apply(extra, collect('superTrend'));
        if (st.showSuperTrend725) {
            view.forEach(function (c) {
                var v = getSt725Value(c);
                if (v != null) extra.push(v);
            });
        }
        if (st.showKeltner) {
            extra.push.apply(extra, collect('keltnerUpperOuter'));
            extra.push.apply(extra, collect('keltnerLowerOuter'));
        }
        if (st.showVwap) {
            view.forEach(function (c) {
                var v = getVwapValue(c);
                if (v != null) extra.push(v);
            });
        }
        function pushEma(getter, enabled) {
            if (!enabled) return;
            view.forEach(function (c) {
                var v = getter(c);
                if (v != null) extra.push(v);
            });
        }
        pushEma(getEma9Value, st.showEma9);
        pushEma(getEma20Value, st.showEma20);
        pushEma(getEma50Value, st.showEma50);
        pushEma(getEma200Value, st.showEma200);
        const maxPrice = Math.max(
            ...highs,
            ...(extra.length ? extra : [Number.MIN_VALUE]),
            ...(levelPrices.length ? levelPrices : [Number.MIN_VALUE]),
            ...(intradayPrices.length ? intradayPrices : [Number.MIN_VALUE])
        );
        const minPrice = Math.min(
            ...lows,
            ...(extra.length ? extra : [Number.MAX_VALUE]),
            ...(levelPrices.length ? levelPrices : [Number.MAX_VALUE]),
            ...(intradayPrices.length ? intradayPrices : [Number.MAX_VALUE])
        );
        return { minPrice, maxPrice, priceRange: (maxPrice - minPrice) || 1 };
    }

    function getVisiblePriceBounds(st, view) {
        const { minPrice, maxPrice, priceRange } = collectViewExtents(st, view);
        const margin = priceRange * 0.04;
        const autoRange = priceRange + margin * 2;
        const mid = (maxPrice + minPrice) / 2;
        const yScale = st.yScale != null ? st.yScale : 1;
        const yPan = st.yPan != null ? st.yPan : 0;
        const visibleRange = autoRange * yScale;
        const visibleMax = mid + visibleRange / 2 + yPan;
        const visibleMin = mid - visibleRange / 2 + yPan;
        return { minPrice, maxPrice, visibleMax, visibleMin, visibleRange, mid, autoRange };
    }

    function scaleYAt(id, factor) {
        const st = states[id];
        if (!st) return;
        const yScale = st.yScale != null ? st.yScale : 1;
        st.yScale = clamp(yScale / factor, Y_SCALE_MIN, Y_SCALE_MAX);
        scheduleRender(id);
    }

    function panYByPixels(id, deltaYPixels) {
        const st = states[id];
        if (!st || deltaYPixels === 0) return;
        const view = getChartView(st);
        if (view.length === 0) return;
        const canvas = st.canvas;
        const container = canvas.parentElement;
        const cssH = Math.max((container ? container.clientHeight : canvas.clientHeight) - 4, 240);
        const chartH = cssH - CHART_PADDING.top - CHART_PADDING.bottom;
        const { visibleRange } = getVisiblePriceBounds(st, view);
        const pricePerPixel = visibleRange / chartH;
        st.yPan = (st.yPan || 0) + deltaYPixels * pricePerPixel;
        scheduleRender(id);
    }

    function ensureInteractions(canvas, id) {
        if (canvas.dataset.pgBound === '1') return;
        canvas.dataset.pgBound = '1';

        const interaction = { dragging: false, mode: null, lastX: 0, lastY: 0 };

        canvas.addEventListener('wheel', function (e) {
            const st = states[id];
            if (!st) return;
            e.preventDefault();
            const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
            if (e.shiftKey || isPriceAxisEvent(canvas, e)) {
                scaleYAt(id, factor);
            } else {
                zoomAt(id, factor);
            }
        }, { passive: false });

        canvas.addEventListener('mousedown', function (e) {
            interaction.dragging = true;
            if (isPriceAxisEvent(canvas, e) || e.shiftKey) {
                interaction.mode = 'yScale';
                interaction.lastY = e.clientY;
            } else if (e.button === 2 || e.altKey) {
                interaction.mode = 'yPan';
                interaction.lastY = e.clientY;
            } else {
                interaction.mode = 'xPan';
                interaction.lastX = e.clientX;
            }
        });

        window.addEventListener('mouseup', function () {
            interaction.dragging = false;
            interaction.mode = null;
        });

        canvas.addEventListener('mousemove', function (e) {
            const st = states[id];
            if (!st) return;

            if (!interaction.dragging) {
                canvas.style.cursor = isPriceAxisEvent(canvas, e) ? 'ns-resize' : 'crosshair';
                return;
            }

            if (interaction.mode === 'yScale') {
                const deltaY = e.clientY - interaction.lastY;
                if (deltaY !== 0) {
                    const yScale = st.yScale != null ? st.yScale : 1;
                    st.yScale = clamp(yScale * Math.pow(1.008, deltaY), Y_SCALE_MIN, Y_SCALE_MAX);
                    interaction.lastY = e.clientY;
                    scheduleRender(id);
                }
                return;
            }

            if (interaction.mode === 'yPan') {
                const deltaY = e.clientY - interaction.lastY;
                if (deltaY !== 0) {
                    panYByPixels(id, deltaY);
                    interaction.lastY = e.clientY;
                }
                return;
            }

            const perBar = (canvas.clientWidth - 72) / st.count;
            const deltaBars = Math.round((e.clientX - interaction.lastX) / perBar);
            if (deltaBars !== 0) {
                st.offset = clamp(st.offset + deltaBars, 0, st.candles.length - st.count);
                interaction.lastX = e.clientX;
                scheduleRender(id);
            }
        });

        canvas.addEventListener('contextmenu', function (e) { e.preventDefault(); });

        var container = canvas.parentElement;
        if (container && typeof ResizeObserver !== 'undefined') {
            var ro = new ResizeObserver(function () {
                if (states[id]) scheduleRender(id);
            });
            ro.observe(container);
        }
    }

    function scheduleRender(id) {
        requestAnimationFrame(function () { render(id); });
    }

    function clamp(v, min, max) { return Math.max(min, Math.min(max, v)); }

    function cssVar(name, fallback) {
        try {
            var root = document.querySelector('.app-shell') || document.documentElement;
            var v = getComputedStyle(root).getPropertyValue(name).trim();
            if (!v)
                v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
            return v || fallback;
        } catch (e) {
            return fallback;
        }
    }

    function chartTheme() {
        return {
            bg: cssVar('--chart-bg', '#121212'),
            grid: cssVar('--chart-grid', '#2A2A2A'),
            axis: cssVar('--chart-axis', '#AAAAAA'),
            axisStrong: cssVar('--chart-axis-strong', '#B8B8B8'),
            labelBg: cssVar('--chart-label-bg', 'rgba(18, 18, 18, 0.82)'),
            accent: cssVar('--gold', '#D4AF37'),
            accentStrong: cssVar('--accent-strong', 'rgba(212, 175, 55, 0.85)')
        };
    }

    function zoomAt(id, factor) {
        const st = states[id];
        if (!st) return;
        const newCount = clamp(Math.round(st.count / factor), 12, st.candles.length);
        st.count = newCount;
        st.offset = clamp(st.offset, 0, st.candles.length - st.count);
        scheduleRender(id);
    }

    function levelColor(level) {
        const g = level.group || '';
        const kind = level.kind || '';
        const theme = chartTheme();
        if (g === 'today') {
            if (kind === 'sell') return 'rgba(255, 82, 82, 0.85)';
            if (kind === 'buy') return 'rgba(0, 200, 83, 0.85)';
            return theme.accentStrong;
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
            return theme.accentStrong;
        }
        return 'rgba(200, 200, 200, 0.55)';
    }

    function parseTime(value) {
        if (value == null) return null;
        if (typeof value === 'number' && !Number.isNaN(value)) return new Date(value);
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

    function segmentHasPivot(seg) {
        if (!seg) return false;
        var pivot = Number(seg.pivot);
        return !Number.isNaN(pivot) && pivot > 0;
    }

    function segmentKey(seg) {
        if (!seg) return '';
        return String(seg.start) + '|' + String(seg.end) + '|' + String(seg.pivot);
    }

    function fillChartBackground(ctx, padding, width, chartH) {
        var left = padding.left;
        var w = width - padding.right - left;
        ctx.fillStyle = chartTheme().bg;
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

    function drawCprStyleLine(ctx, x0, x1, y, label, kind) {
        var color = levelColor({ group: 'cpr', kind: kind });
        ctx.strokeStyle = color;
        ctx.lineWidth = 1;
        ctx.setLineDash([8, 4]);
        ctx.beginPath();
        ctx.moveTo(x0, y);
        ctx.lineTo(x1, y);
        ctx.stroke();
        ctx.setLineDash([]);
        drawLevelLabel(ctx, x0 + 2, y, label, color);
    }

    function normalizeCprSegment(raw) {
        if (!raw) return raw;
        return {
            start: field(raw, 'start'),
            end: field(raw, 'end'),
            pivot: num(field(raw, 'pivot')),
            tc: num(field(raw, 'tc')),
            bc: num(field(raw, 'bc'))
        };
    }

    function drawIntradaCprLevels(ctx, view, segments, padding, chartH, slot, toY, toX) {
        if (!segments || segments.length === 0 || !view.length) return;

        segments.forEach(function (seg) {
            if (!segmentHasPivot(seg)) return;

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

            drawCprStyleLine(ctx, x0, x1, toY(Number(seg.tc)), 'TC', 'sell');
            drawCprStyleLine(ctx, x0, x1, toY(Number(seg.pivot)), 'CPR', 'neutral');
            drawCprStyleLine(ctx, x0, x1, toY(Number(seg.bc)), 'BC', 'buy');

            ctx.strokeStyle = 'rgba(0, 200, 83, 0.25)';
            ctx.lineWidth = 1;
            ctx.setLineDash([]);
            ctx.beginPath();
            ctx.moveTo(x0, padding.top);
            ctx.lineTo(x0, padding.top + chartH);
            ctx.stroke();
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

        ctx.fillStyle = chartTheme().labelBg;
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

    function patternColor(bias) {
        if (bias === 'buy') return '#00C853';
        if (bias === 'sell') return '#FF5252';
        return '#FFB300';
    }

    function drawPatternMarkers(ctx, view, padding, chartH, toX, toY, candleWidth) {
        if (!view || view.length === 0) return;
        var labelH = 12;
        ctx.save();
        ctx.font = 'bold 10px "Segoe UI", sans-serif';
        ctx.textBaseline = 'middle';
        for (var i = 0; i < view.length; i++) {
            var c = view[i];
            var label = c.patternLabel || c.patternCode;
            if (!label) continue;
            var bias = (c.patternBias || 'neutral').toLowerCase();
            var color = patternColor(bias);
            var x = toX(i);
            var above = bias === 'sell';
            var tipY = above ? toY(c.high) - 6 : toY(c.low) + 6;
            var textY = above ? tipY - 12 : tipY + 12;

            // Keep labels inside chart area
            if (above && textY < padding.top + 8) textY = padding.top + 8;
            if (!above && textY > padding.top + chartH - 8) textY = padding.top + chartH - 8;

            // Marker triangle
            ctx.fillStyle = color;
            ctx.beginPath();
            if (above) {
                ctx.moveTo(x, tipY);
                ctx.lineTo(x - 4, tipY - 7);
                ctx.lineTo(x + 4, tipY - 7);
            } else {
                ctx.moveTo(x, tipY);
                ctx.lineTo(x - 4, tipY + 7);
                ctx.lineTo(x + 4, tipY + 7);
            }
            ctx.closePath();
            ctx.fill();

            // Compact label chip
            var text = String(label);
            var tw = ctx.measureText(text).width;
            var bx = x - tw / 2 - 3;
            var by = textY - labelH / 2;
            ctx.fillStyle = chartTheme().labelBg;
            ctx.fillRect(bx, by, tw + 6, labelH);
            ctx.strokeStyle = color;
            ctx.lineWidth = 1;
            ctx.strokeRect(bx, by, tw + 6, labelH);
            ctx.fillStyle = color;
            ctx.textAlign = 'center';
            ctx.fillText(text, x, textY);
        }
        ctx.restore();
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
        if (!canvas || !candles || candles.length === 0) return false;

        const normalized = candles.map(normalizeCandle);
        const prev = states[canvasId];
        const opts = overlayOptions || {};
        const showPoc = overlayFlag(opts, 'showPoc', true);
        const showPivot = overlayFlag(opts, 'showPivot', true);
        const showCamarilla = overlayFlag(opts, 'showCamarilla', true);
        const intradayCprSegments = (opts.intradayCpr || opts.IntradayCpr || []).map(normalizeCprSegment);
        const showIntradaCpr = overlayFlag(opts, 'showIntradaCpr', false)
            && intradayCprSegments.length > 0;
        const showKeltner = overlayFlag(opts, 'showKeltner', false);
        const showVwap = overlayFlag(opts, 'showVwap', false);
        const showSuperTrend = overlayFlag(opts, 'showSuperTrend', false);
        const showSuperTrend725 = overlayFlag(opts, 'showSuperTrend725', false);
        const showEma9 = overlayFlag(opts, 'showEma9', false);
        const showEma20 = overlayFlag(opts, 'showEma20', false);
        const showEma50 = overlayFlag(opts, 'showEma50', false);
        const showEma200 = overlayFlag(opts, 'showEma200', false);
        const showPatterns = overlayFlag(opts, 'showPatterns', false);

        ensureStudyIndicators(normalized, {
            showVwap: showVwap,
            showEma9: showEma9,
            showEma20: showEma20,
            showEma50: showEma50,
            showEma200: showEma200,
            showSuperTrend725: showSuperTrend725,
            showSuperTrend: showSuperTrend
        });

        let count, offset, yScale, yPan;
        if (prev && prev.timeframe === timeframe) {
            count = clamp(prev.count, 12, normalized.length);
            offset = clamp(prev.offset, 0, normalized.length - count);
            yScale = prev.yScale != null ? prev.yScale : 1;
            yPan = prev.yPan != null ? prev.yPan : 0;
        } else {
            count = normalized.length;
            offset = 0;
            yScale = 1;
            yPan = 0;
        }

        states[canvasId] = {
            canvas, candles: normalized, timeframe, count, offset,
            yScale, yPan,
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
            showSuperTrend725: showSuperTrend725,
            showEma9: showEma9,
            showEma20: showEma20,
            showEma50: showEma50,
            showEma200: showEma200,
            showPatterns: showPatterns
        };
        ensureInteractions(canvas, canvasId);
        scheduleRender(canvasId);
        return true;
    }

    function zoom(canvasId, factor) { zoomAt(canvasId, factor); }

    function resetZoom(canvasId) {
        const st = states[canvasId];
        if (!st) return;
        st.count = st.candles.length;
        st.offset = 0;
        st.yScale = 1;
        st.yPan = 0;
        scheduleRender(canvasId);
    }

    function render(id) {
        const st = states[id];
        if (!st) return;
        const { canvas, candles, timeframe } = st;

        const container = canvas.parentElement;
        const cssW = Math.max((container ? container.clientWidth : canvas.clientWidth) - 4, 320);
        const cssH = Math.max((container ? container.clientHeight : canvas.clientHeight) - 4, 240);

        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(cssW * dpr);
        canvas.height = Math.round(cssH * dpr);
        canvas.style.width = cssW + 'px';
        canvas.style.height = cssH + 'px';

        const ctx = canvas.getContext('2d');
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        const width = cssW;
        const height = cssH;
        const padding = CHART_PADDING;
        const chartW = width - padding.left - padding.right;
        const chartH = height - padding.top - padding.bottom;

        const theme = chartTheme();
        ctx.clearRect(0, 0, width, height);
        ctx.fillStyle = theme.bg;
        ctx.fillRect(0, 0, width, height);

        const view = getChartView(st);
        if (view.length === 0) return;

        const { visibleMax, visibleMin, visibleRange } = getVisiblePriceBounds(st, view);
        const slot = chartW / view.length;
        const candleWidth = Math.max(1.5, slot - 2);

        const toY = (price) => padding.top + ((visibleMax - price) / visibleRange) * chartH;
        const toX = (i) => padding.left + slot * i + slot / 2;

        if (st.showPoc && st.pocToday && !st.showIntradaCpr) {
            drawPocBackground(ctx, st.pocToday, padding, width, chartH, toY, true);
        } else {
            fillChartBackground(ctx, padding, width, chartH);
        }

        ctx.textBaseline = 'middle';
        for (let i = 0; i <= 4; i++) {
            const y = padding.top + (chartH / 4) * i;
            ctx.strokeStyle = theme.grid;
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(padding.left, y);
            ctx.lineTo(width - padding.right, y);
            ctx.stroke();

            const price = visibleMax - (visibleRange / 4) * i;
            ctx.fillStyle = theme.axis;
            ctx.font = '12px "Segoe UI", sans-serif';
            ctx.textAlign = 'left';
            ctx.fillText(price.toFixed(2), width - padding.right + 6, y);
        }

        ctx.textBaseline = 'alphabetic';
        const labelCount = Math.min(7, view.length);
        const step = Math.max(1, Math.floor(view.length / labelCount));
        ctx.fillStyle = theme.axisStrong;
        ctx.font = '12px "Segoe UI", sans-serif';
        ctx.textAlign = 'center';
        let lastDay = null;
        for (let i = 0; i < view.length; i += step) {
            const t = view[i].time ? new Date(view[i].time) : null;
            if (!t) continue;
            const x = toX(i);
            const day = t.getDate();
            let label;
            if (timeframe === '1D' || timeframe === '1W') {
                label = t.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: timeframe === '1W' ? '2-digit' : undefined });
            } else if (day !== lastDay) {
                label = t.toLocaleDateString('en-IN', { day: '2-digit', month: 'short' });
                lastDay = day;
            } else {
                label = t.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: false });
            }
            ctx.fillText(label, x, height - 10);
        }

        const drawStudyLine = (getValue, color, dash, width) => {
            ctx.strokeStyle = color;
            ctx.lineWidth = width || 2;
            ctx.setLineDash(dash || []);
            ctx.beginPath();
            let started = false;
            view.forEach((c, i) => {
                const v = getValue(c);
                if (v == null || Number.isNaN(v)) { started = false; return; }
                const x = toX(i), y = toY(v);
                if (!started) { ctx.moveTo(x, y); started = true; }
                else ctx.lineTo(x, y);
            });
            ctx.stroke();
            ctx.setLineDash([]);
        };

        const drawLine = (key, color, dash, width) => {
            ctx.strokeStyle = color;
            ctx.lineWidth = width || 2;
            ctx.setLineDash(dash || []);
            ctx.beginPath();
            let started = false;
            view.forEach((c, i) => {
                const v = c[key];
                if (v == null || Number.isNaN(v)) { started = false; return; }
                const x = toX(i), y = toY(v);
                if (!started) { ctx.moveTo(x, y); started = true; }
                else ctx.lineTo(x, y);
            });
            ctx.stroke();
            ctx.setLineDash([]);
        };

        const drawSuperTrendSegments = (getValue, lineWidth, colors, dash) => {
            ctx.lineWidth = lineWidth || 2;
            ctx.setLineDash(dash || []);
            var upColor = (colors && colors.up) || '#00C853';
            var downColor = (colors && colors.down) || '#FF5252';
            let segment = null;
            const flush = () => {
                if (!segment || segment.points.length < 1) { segment = null; return; }
                ctx.strokeStyle = segment.color;
                ctx.beginPath();
                segment.points.forEach((p, idx) => idx === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y));
                ctx.stroke();
                segment = null;
            };
            view.forEach((candle, i) => {
                const stv = getValue(candle);
                if (stv == null || Number.isNaN(stv)) { flush(); return; }
                const close = candle.close;
                const color = close >= stv ? upColor : downColor;
                const point = { x: toX(i), y: toY(stv) };
                if (!segment || segment.color !== color) { flush(); segment = { color: color, points: [point] }; }
                else segment.points.push(point);
            });
            flush();
            ctx.setLineDash([]);
        };

        // Day CPR / POC / Camarilla — full-width dashed lines + labels on chart
        drawLevels(ctx, filterLevels(st), padding, width, toY);

        view.forEach((candle, i) => {
            const open = candle.open;
            const close = candle.close;
            const high = candle.high;
            const low = candle.low;
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

        if (st.showPatterns) {
            drawPatternMarkers(ctx, view, padding, chartH, toX, toY, candleWidth);
        }

        // 1m CPR — same dashed line + label style as Day CPR, per 15m window on chart
        if (st.showIntradaCpr && st.intradayCprSegments && st.intradayCprSegments.length > 0) {
            drawIntradaCprLevels(ctx, view, st.intradayCprSegments, padding, chartH, slot, toY, toX);
        }

        // Study overlays drawn after candles so lines stay visible on top of bodies.
        if (st.showKeltner) {
            drawLine('keltnerUpperOuter', 'rgba(120,144,255,0.55)');
            drawLine('keltnerUpperInner', 'rgba(120,144,255,0.35)');
            drawLine('keltnerMid', 'rgba(120,144,255,0.45)', [4, 3]);
            drawLine('keltnerLowerInner', 'rgba(120,144,255,0.35)');
            drawLine('keltnerLowerOuter', 'rgba(120,144,255,0.55)');
        }

        // ST overlays first; VWAP / EMA drawn on top (same pattern as ST7 over ST10).
        if (st.showSuperTrend) {
            drawSuperTrendSegments(function (c) { return c.superTrend; }, 2.25);
        }

        if (st.showSuperTrend725) {
            drawSuperTrendSegments(
                getSt725Value,
                2.5,
                { up: '#00BCD4', down: '#FF9800' },
                [6, 3]
            );
        }

        if (st.showVwap) {
            drawStudyLine(getVwapValue, theme.accent, [4, 3], 2.5);
        }

        if (st.showEma9) {
            drawStudyLine(getEma9Value, 'rgba(171, 71, 188, 0.95)', [2, 2], 2.0);
        }
        if (st.showEma20) {
            drawStudyLine(getEma20Value, 'rgba(255, 152, 0, 0.95)', [8, 4], 2.5);
        }
        if (st.showEma50) {
            drawStudyLine(getEma50Value, 'rgba(66, 165, 245, 0.95)', [6, 3], 2.25);
        }
        if (st.showEma200) {
            drawStudyLine(getEma200Value, 'rgba(38, 166, 154, 0.95)', [10, 4], 2.5);
        }
    }

    function updateOverlayOptions(canvasId, overlayOptions, overlayInts) {
        const st = states[canvasId];
        if (!st) return false;
        const opts = mergeOverlayInts(overlayOptions, overlayInts);
        st.showPoc = overlayFlag(opts, 'showPoc', st.showPoc);
        st.showPivot = overlayFlag(opts, 'showPivot', st.showPivot);
        st.showCamarilla = overlayFlag(opts, 'showCamarilla', st.showCamarilla);
        st.showIntradaCpr = overlayFlag(opts, 'showIntradaCpr', false)
            && (opts.intradayCpr || opts.IntradayCpr || st.intradayCprSegments || []).length > 0;
        st.showKeltner = overlayFlag(opts, 'showKeltner', false);
        st.showVwap = overlayFlag(opts, 'showVwap', false);
        st.showSuperTrend = overlayFlag(opts, 'showSuperTrend', false);
        st.showSuperTrend725 = overlayFlag(opts, 'showSuperTrend725', false);
        st.showEma9 = overlayFlag(opts, 'showEma9', false);
        st.showEma20 = overlayFlag(opts, 'showEma20', false);
        st.showEma50 = overlayFlag(opts, 'showEma50', false);
        st.showEma200 = overlayFlag(opts, 'showEma200', false);
        st.showPatterns = overlayFlag(opts, 'showPatterns', false);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function setPatternsOverlay(canvasId, show) {
        const st = states[canvasId];
        if (!st) return false;
        st.showPatterns = intOverlayFlag(show);
        scheduleRender(canvasId);
        return true;
    }

    function setSt725Overlay(canvasId, show) {
        const st = states[canvasId];
        if (!st) return false;
        st.showSuperTrend725 = intOverlayFlag(show);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function setVwapOverlay(canvasId, show) {
        const st = states[canvasId];
        if (!st) return false;
        st.showVwap = intOverlayFlag(show);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function setEma20Overlay(canvasId, show) {
        const st = states[canvasId];
        if (!st) return false;
        st.showEma20 = intOverlayFlag(show);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function setEmaOverlays(canvasId, showEma9, showEma20, showEma50, showEma200) {
        const st = states[canvasId];
        if (!st) return false;
        st.showEma9 = intOverlayFlag(showEma9);
        st.showEma20 = intOverlayFlag(showEma20);
        st.showEma50 = intOverlayFlag(showEma50);
        st.showEma200 = intOverlayFlag(showEma200);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function setIntradayCprOverlay(canvasId, show, segmentsJson) {
        const st = states[canvasId];
        if (!st) return false;
        st.showIntradaCpr = intOverlayFlag(show);
        if (segmentsJson != null) {
            var segs = typeof segmentsJson === 'string' ? JSON.parse(segmentsJson) : segmentsJson;
            st.intradayCprSegments = (segs || []).map(normalizeCprSegment);
            if (st.intradayCprSegments.length === 0)
                st.showIntradaCpr = false;
        } else if (!st.intradayCprSegments || st.intradayCprSegments.length === 0) {
            st.showIntradaCpr = false;
        }
        scheduleRender(canvasId);
        return true;
    }

    function setStudyOverlays(canvasId, showVwap, showEma20, showSt725, showSt103) {
        const st = states[canvasId];
        if (!st) return false;
        st.showVwap = intOverlayFlag(showVwap);
        st.showEma20 = intOverlayFlag(showEma20);
        st.showSuperTrend725 = intOverlayFlag(showSt725);
        st.showSuperTrend = intOverlayFlag(showSt103);
        ensureStudyIndicators(st.candles, studyFlagsFromState(st));
        scheduleRender(canvasId);
        return true;
    }

    function hasState(canvasId) {
        return !!states[canvasId];
    }

    function refreshAll() {
        Object.keys(states).forEach(function (id) {
            scheduleRender(id);
        });
    }

    window.pgOneMergeOverlayInts = mergeOverlayInts;

    function isReady() {
        return typeof setData === 'function';
    }

    return {
        isReady: isReady,
        drawCandlestickChart: setData,
        updateOverlayOptions: updateOverlayOptions,
        setStudyOverlays: setStudyOverlays,
        setSt725Overlay: setSt725Overlay,
        setVwapOverlay: setVwapOverlay,
        setEma20Overlay: setEma20Overlay,
        setEmaOverlays: setEmaOverlays,
        setPatternsOverlay: setPatternsOverlay,
        setIntradayCprOverlay: setIntradayCprOverlay,
        hasState: hasState,
        refreshAll: refreshAll,
        zoom: zoom,
        resetZoom: resetZoom
    };
})();

window.pgOneChartReady = function () {
    return window.pgOneChart && typeof window.pgOneChart.drawCandlestickChart === 'function';
};

window.pgOneChartHasState = function (canvasId) {
    return window.pgOneChart && window.pgOneChart.hasState(canvasId);
};

window.pgOneSetSt725Overlay = function (canvasId, show) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setSt725Overlay(canvasId, show);
    } catch (err) {
        console.error('pgOneSetSt725Overlay failed', err);
        return false;
    }
};

window.pgOneSetVwapOverlay = function (canvasId, show) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setVwapOverlay(canvasId, show);
    } catch (err) {
        console.error('pgOneSetVwapOverlay failed', err);
        return false;
    }
};

window.pgOneSetEma20Overlay = function (canvasId, show) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setEma20Overlay(canvasId, show);
    } catch (err) {
        console.error('pgOneSetEma20Overlay failed', err);
        return false;
    }
};

window.pgOneSetEmaOverlays = function (canvasId, showEma9, showEma20, showEma50, showEma200) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setEmaOverlays(canvasId, showEma9, showEma20, showEma50, showEma200);
    } catch (err) {
        console.error('pgOneSetEmaOverlays failed', err);
        return false;
    }
};

window.pgOneSetPatternsOverlay = function (canvasId, show) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setPatternsOverlay(canvasId, show);
    } catch (err) {
        console.error('pgOneSetPatternsOverlay failed', err);
        return false;
    }
};

window.pgOneSetIntradayCprOverlay = function (canvasId, show, segmentsJson) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setIntradayCprOverlay(canvasId, show, segmentsJson);
    } catch (err) {
        console.error('pgOneSetIntradayCprOverlay failed', err);
        return false;
    }
};

window.pgOneSetStudyOverlays = function (canvasId, showVwap, showEma20, showSt725, showSt103) {
    try {
        if (!window.pgOneChartReady()) return false;
        return window.pgOneChart.setStudyOverlays(canvasId, showVwap, showEma20, showSt725, showSt103);
    } catch (err) {
        console.error('pgOneSetStudyOverlays failed', err);
        return false;
    }
};

window.pgOneDrawCandles = function (
    canvasId,
    candlesJson,
    timeframe,
    levelsJson,
    pocToday,
    overlaysJson,
    showVwapInt,
    showEma20Int,
    showSt725Int,
    showSt103Int
) {
    try {
        if (!window.pgOneChartReady()) return false;
        var overlayOptions = typeof overlaysJson === 'string' ? JSON.parse(overlaysJson) : (overlaysJson || {});
        overlayOptions = window.pgOneMergeOverlayInts(overlayOptions, {
            showVwap: showVwapInt,
            showEma20: showEma20Int,
            showSuperTrend725: showSt725Int,
            showSuperTrend: showSt103Int
        });
        var candles = typeof candlesJson === 'string' ? JSON.parse(candlesJson) : candlesJson;
        var levels = typeof levelsJson === 'string' ? JSON.parse(levelsJson) : (levelsJson || []);
        return window.pgOneChart.drawCandlestickChart(canvasId, candles, timeframe, levels, pocToday, overlayOptions);
    } catch (err) {
        console.error('pgOneDrawCandles failed', err);
        return false;
    }
};
