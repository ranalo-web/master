// Show / hide password: every password box on every page gets an eye button
// on its right. Tap to see what's typed, tap again to hide it. Loaded by all
// the layouts, so new password boxes get it without any extra markup.
// Boxes are switched back to hidden when their form is submitted, so the
// browser and password managers still treat them as passwords.
(function () {
    'use strict';

    function icon(name) {
        var em = document.createElement('em');
        em.className = 'icon ni ' + name;
        return em;
    }

    function addToggle(input) {
        if (input.dataset.pwToggle) return;
        input.dataset.pwToggle = '1';

        // Wrap the box so the button can sit inside its right edge. The
        // box's bottom margin moves to the wrapper so the button stays centred.
        var wrap = document.createElement('div');
        wrap.style.position = 'relative';
        ['mb-1', 'mb-2', 'mb-3'].forEach(function (c) {
            if (input.classList.contains(c)) { input.classList.remove(c); wrap.classList.add(c); }
        });
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
        input.style.paddingRight = '2.75rem';

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.tabIndex = -1;
        btn.setAttribute('aria-label', 'Show password');
        btn.title = 'Show password';
        btn.style.cssText = 'position:absolute;top:0;right:0;height:100%;width:2.75rem;border:0;background:none;'
            + 'display:flex;align-items:center;justify-content:center;color:#8094ae;font-size:1.1rem;cursor:pointer;z-index:3;';
        btn.appendChild(icon('ni-eye'));
        wrap.appendChild(btn);

        function set(show) {
            input.type = show ? 'text' : 'password';
            btn.replaceChild(icon(show ? 'ni-eye-off' : 'ni-eye'), btn.firstChild);
            btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            btn.title = show ? 'Hide password' : 'Show password';
        }

        btn.addEventListener('click', function (e) {
            e.preventDefault();
            set(input.type === 'password');
            input.focus();
        });
        if (input.form) {
            input.form.addEventListener('submit', function () { set(false); });
        }
    }

    function init() {
        document.querySelectorAll('input[type="password"]').forEach(addToggle);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
