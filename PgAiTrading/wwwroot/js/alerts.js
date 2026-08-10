window.pgAiTradingAlerts = {
    /**
     * Good-to-trade alert — deliberately different from PGCryptoTrading's rising sine chime.
     * Equity desk pattern: two sharp pulses + sustained confirm tone. Louder master gain.
     */
    playGoodToTrade: function () {
        try {
            var AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext)
                return;

            var ctx = new AudioContext();
            var now = ctx.currentTime;
            var master = ctx.createGain();
            // Louder than PGCrypto (~0.85) — near full scale with headroom for stacking tones.
            master.gain.value = 1.0;
            master.connect(ctx.destination);

            function playTone(type, freq, start, duration, peak) {
                var osc = ctx.createOscillator();
                var gain = ctx.createGain();
                osc.type = type;
                osc.frequency.value = freq;
                gain.gain.setValueAtTime(0.0001, start);
                gain.gain.exponentialRampToValueAtTime(peak, start + 0.015);
                gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
                osc.connect(gain);
                gain.connect(master);
                osc.start(start);
                osc.stop(start + duration + 0.02);
            }

            // Pulse 1 + 2 (square — punchy, distinct from crypto sine)
            playTone('square', 620, now, 0.12, 0.95);
            playTone('square', 620, now + 0.16, 0.12, 0.95);
            // Confirm chord (triangle + sine overlay)
            playTone('triangle', 784, now + 0.36, 0.42, 1.0);
            playTone('sine', 1175, now + 0.36, 0.42, 0.72);

            window.setTimeout(function () {
                ctx.close();
            }, 1200);
        } catch (e) {
            console.warn('pgAiTradingAlerts.playGoodToTrade failed', e);
        }
    }
};
