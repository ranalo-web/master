// IMEI scanner: reads the IMEI barcode on the phone's box or on the *#06#
// screen with the device camera, so dealers don't type 15 digits by hand.
//
//   <button type="button" data-imei-scan="#imei">          fills one input
//   <button type="button" data-imei-scan="#imeis" data-imei-multi>
//                                                          adds lines to a textarea
//   <input data-imei-check>                                warns on a typo
//
// Only 15-digit numbers with a valid check digit are taken (the same rule as
// DeviceLockRules.IsValidImei), so serial numbers, EANs and model codes on
// the same box are ignored. Dual-SIM boxes show IMEI 1 and IMEI 2: when both
// are seen the dealer taps IMEI 1, and with data-imei-second="#imei2" on the
// button the other one fills that input.
//
// Uses the browser's own BarcodeDetector where it has one (Chrome on
// Android), otherwise ZXing from a CDN. The camera needs HTTPS.
(function () {
    'use strict';

    var ZXING_URL = 'https://unpkg.com/@zxing/library@0.20.0/umd/index.min.js';
    var FORMATS = ['code_128', 'code_39', 'ean_13', 'qr_code', 'data_matrix'];
    var SETTLE_MS = 900; // keep looking this long after the first IMEI, for IMEI 2

    function isValidImei(s) {
        if (!/^\d{15}$/.test(s)) return false;
        var sum = 0;
        for (var i = 0; i < 15; i++) {
            var d = s.charCodeAt(i) - 48;
            if (i % 2 === 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 === 0;
    }

    // Every valid IMEI in a decoded barcode. QR codes on some boxes and
    // *#06# screens hold several numbers with labels around them.
    function imeisIn(text) {
        var found = [];
        (String(text || '').match(/\d{15,17}/g) || []).forEach(function (run) {
            // 16-17 digit runs: IMEISV or a prefix; try every 15-digit window.
            for (var i = 0; i + 15 <= run.length; i++) {
                var c = run.substr(i, 15);
                if (isValidImei(c) && found.indexOf(c) < 0) found.push(c);
            }
        });
        return found;
    }

    var zxingLoading = null;
    function loadZxing() {
        if (window.ZXing) return Promise.resolve(window.ZXing);
        if (zxingLoading) return zxingLoading;
        zxingLoading = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = ZXING_URL;
            s.onload = function () { window.ZXing ? resolve(window.ZXing) : reject(new Error('ZXing missing')); };
            s.onerror = function () { zxingLoading = null; reject(new Error('Could not load the scanner')); };
            document.head.appendChild(s);
        });
        return zxingLoading;
    }

    function nativeDetector() {
        if (!('BarcodeDetector' in window)) return Promise.resolve(null);
        return window.BarcodeDetector.getSupportedFormats()
            .then(function (supported) {
                var formats = FORMATS.filter(function (f) { return supported.indexOf(f) >= 0; });
                return formats.indexOf('code_128') >= 0 ? new window.BarcodeDetector({ formats: formats }) : null;
            })
            .catch(function () { return null; });
    }

    function css(el, styles) { for (var k in styles) el.style[k] = styles[k]; return el; }
    function make(tag, styles, text) {
        var el = css(document.createElement(tag), styles || {});
        if (text != null) el.textContent = text;
        return el;
    }
    function button(text, primary) {
        var b = make('button', { minHeight: '44px', padding: '0 18px', borderRadius: '6px', fontSize: '15px', fontWeight: '600',
            border: primary ? '0' : '1px solid rgba(255,255,255,.5)', background: primary ? '#1ee0ac' : 'transparent', color: primary ? '#0b2e24' : '#fff' }, text);
        b.type = 'button';
        return b;
    }

    function setValue(input, value) {
        input.value = value;
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.dispatchEvent(new Event('blur'));
    }

    // second: optional input for the other IMEI on a dual-SIM phone
    // (data-imei-second on the button).
    function open(target, multi, second) {
        if (!window.isSecureContext || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alertBox('The camera only works on the secure (https://) site. Type the IMEI instead.');
            return;
        }

        var overlay = make('div', { position: 'fixed', inset: '0', zIndex: '2000', background: '#000', color: '#fff',
            display: 'flex', flexDirection: 'column', fontFamily: 'inherit' });
        var video = make('video', { flex: '1', width: '100%', minHeight: '0', objectFit: 'cover', background: '#000' });
        video.setAttribute('playsinline', '');
        video.muted = true;
        var frame = make('div', { position: 'absolute', left: '8%', right: '8%', top: '35%', height: '22%', border: '3px solid #1ee0ac',
            borderRadius: '10px', boxShadow: '0 0 0 9999px rgba(0,0,0,.35)', pointerEvents: 'none' });
        var panel = make('div', { padding: '14px 16px calc(14px + env(safe-area-inset-bottom))', background: 'rgba(20,20,30,.96)' });
        var status = make('div', { fontSize: '15px', marginBottom: '10px' }, 'Starting the camera…');
        var picks = make('div', { display: 'flex', flexDirection: 'column', gap: '8px', marginBottom: '10px' });
        var added = make('div', { fontSize: '13px', opacity: '.8', marginBottom: '10px' });
        var actions = make('div', { display: 'flex', gap: '10px', justifyContent: 'flex-end' });
        var close = button(multi ? 'Done' : 'Cancel', multi);
        actions.appendChild(close);
        panel.appendChild(status);
        panel.appendChild(picks);
        if (multi) panel.appendChild(added);
        panel.appendChild(actions);
        var stage = make('div', { position: 'relative', flex: '1', display: 'flex', minHeight: '0' });
        stage.appendChild(video);
        stage.appendChild(frame);
        overlay.appendChild(stage);
        overlay.appendChild(panel);
        document.body.appendChild(overlay);

        var stream = null, reader = null, timer = null, settle = null, stopped = false;
        var seen = [], addedCount = 0;

        function stop() {
            stopped = true;
            clearTimeout(timer);
            clearTimeout(settle);
            if (reader) { try { reader.reset(); } catch (e) { } }
            if (stream) stream.getTracks().forEach(function (t) { t.stop(); });
            if (overlay.parentNode) overlay.parentNode.removeChild(overlay);
        }
        close.addEventListener('click', stop);

        function fill(imei) {
            setValue(target, imei);
            var other = seen.filter(function (s) { return s !== imei; })[0];
            if (second && other) setValue(second, other);
            stop();
        }

        function addLine(imei) {
            var lines = target.value.split(/\r?\n/).map(function (l) { return l.trim(); }).filter(Boolean);
            if (lines.indexOf(imei) >= 0) return false;
            lines.push(imei);
            target.value = lines.join('\n');
            target.dispatchEvent(new Event('input', { bubbles: true }));
            return true;
        }

        function offerChoice() {
            status.textContent = seen.length < 2 ? 'IMEI found:'
                : second ? 'Two IMEIs found (dual SIM). Tap IMEI 1 (listed first on the box); the other is saved as IMEI 2:'
                : 'Two IMEIs found (dual SIM). Tap the one to use:';
            picks.innerHTML = '';
            seen.forEach(function (imei, i) {
                var b = button((seen.length > 1 ? 'IMEI ' + (i + 1) + ': ' : '') + imei, true);
                b.style.fontFamily = 'monospace';
                b.style.fontSize = '17px';
                b.addEventListener('click', function () { fill(imei); });
                picks.appendChild(b);
            });
        }

        function onText(text) {
            if (stopped) return;
            imeisIn(text).forEach(function (imei) {
                if (multi) {
                    if (addLine(imei)) {
                        addedCount++;
                        if (navigator.vibrate) navigator.vibrate(80);
                        status.textContent = 'Added ' + imei + '. Scan the next phone, or tap Done.';
                        added.textContent = addedCount + ' added this time.';
                    }
                    return;
                }
                if (seen.indexOf(imei) >= 0) return;
                seen.push(imei);
                if (navigator.vibrate) navigator.vibrate(80);
                if (seen.length === 1) {
                    status.textContent = 'Found ' + imei + '…';
                    // A box with IMEI 1 and IMEI 2: wait briefly for the second.
                    settle = setTimeout(function () { seen.length === 1 ? fill(seen[0]) : offerChoice(); }, SETTLE_MS);
                } else {
                    clearTimeout(settle);
                    offerChoice();
                }
            });
        }

        var hint = multi
            ? 'Point at the IMEI barcode on each box. Each one is added to the list.'
            : 'Point at the IMEI barcode on the box, or dial *#06# on the phone and scan the screen.';

        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } }, audio: false })
            .then(function (s) {
                stream = s;
                if (stopped) { stop(); return; }
                video.srcObject = s;
                return video.play().then(function () { return nativeDetector(); });
            })
            .then(function (detector) {
                if (stopped || !stream) return;
                status.textContent = hint;
                if (detector) {
                    (function tick() {
                        if (stopped) return;
                        detector.detect(video)
                            .then(function (codes) { codes.forEach(function (c) { onText(c.rawValue); }); })
                            .catch(function () { })
                            .then(function () { timer = setTimeout(tick, 120); });
                    })();
                    return;
                }
                return loadZxing().then(function (ZXing) {
                    if (stopped) return;
                    var hints = new Map();
                    hints.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, [ZXing.BarcodeFormat.CODE_128, ZXing.BarcodeFormat.CODE_39,
                        ZXing.BarcodeFormat.QR_CODE, ZXing.BarcodeFormat.DATA_MATRIX]);
                    hints.set(ZXing.DecodeHintType.TRY_HARDER, true);
                    reader = new ZXing.BrowserMultiFormatReader(hints, 150);
                    // ZXing opens its own stream on the same video element.
                    stream.getTracks().forEach(function (t) { t.stop(); });
                    stream = null;
                    return reader.decodeFromConstraints({ video: { facingMode: { ideal: 'environment' }, width: { ideal: 1920 }, height: { ideal: 1080 } } }, video,
                        function (result) { if (result) onText(result.getText()); });
                });
            })
            .catch(function (err) {
                if (stopped) return;
                var denied = err && (err.name === 'NotAllowedError' || err.name === 'SecurityError');
                status.textContent = denied
                    ? 'Camera access was blocked. Allow the camera for this site in the browser settings, or type the IMEI.'
                    : 'The scanner could not start (' + (err && err.message || 'unknown error') + '). Type the IMEI instead.';
            });
    }

    function alertBox(message) {
        var note = make('div', { position: 'fixed', left: '16px', right: '16px', bottom: '16px', zIndex: '2000', padding: '14px 16px',
            borderRadius: '8px', background: '#e85347', color: '#fff', fontSize: '14px' }, message);
        document.body.appendChild(note);
        setTimeout(function () { if (note.parentNode) note.parentNode.removeChild(note); }, 6000);
    }

    // Typed IMEIs: flag a wrong length or check digit before submitting.
    function wireCheck(input) {
        var note = make('div', { color: '#e85347', fontSize: '12px', marginTop: '4px', display: 'none' });
        (input.closest('.form-control-wrap') || input).insertAdjacentElement('afterend', note);
        function check() {
            var v = input.value.replace(/\s+/g, '');
            var bad = v.length > 0 && !isValidImei(v);
            note.textContent = !bad ? '' : v.length !== 15 || !/^\d+$/.test(v)
                ? 'An IMEI is 15 digits; this has ' + v.length + (/^\d*$/.test(v) ? '.' : ' and letters or symbols.')
                : 'This IMEI\'s check digit is wrong, so a digit is mistyped. Scan it instead.';
            note.style.display = bad ? 'block' : 'none';
            input.classList.toggle('is-invalid', bad);
        }
        input.addEventListener('blur', check);
        input.addEventListener('input', function () { if (note.style.display === 'block') check(); });
        if (input.value) check();
    }

    function init() {
        document.querySelectorAll('[data-imei-scan]').forEach(function (btn) {
            var target = document.querySelector(btn.getAttribute('data-imei-scan'));
            if (!target) return;
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var secondSel = btn.getAttribute('data-imei-second');
                open(target, btn.hasAttribute('data-imei-multi'), secondSel ? document.querySelector(secondSel) : null);
            });
        });
        document.querySelectorAll('[data-imei-check]').forEach(wireCheck);
    }

    window.RanaloImei = { isValid: isValidImei, findIn: imeisIn };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
