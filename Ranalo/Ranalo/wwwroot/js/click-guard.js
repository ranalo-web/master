// Stops repeat clicks sending the same request twice, and shows a thin bar
// while the next page loads so people can see the click registered.
//
//  - Forms: after the first submit, a second submit of the same form is
//    ignored and its buttons are disabled. Covers double-clicks and Enter
//    pressed twice. A submit a page cancels itself (a "Cancel" on a confirm
//    box, a validation message) doesn't count.
//  - Links: a second click on the link that's already loading is ignored.
//    Clicking a DIFFERENT link still works: the browser drops the first page
//    and the server stops its queries (PageLoadMiddleware).
//  - Downloads leave the page where it is, so everything is released again
//    after a few seconds, and when the page is shown again from Back.
(function () {
    'use strict';
    var RELEASE_AFTER_MS = 10000;
    var pendingHref = null;
    var releaseTimer = null;

    var bar = null;
    function showBar() {
        if (!document.body) return;
        if (!bar) {
            var css = document.createElement('style');
            css.textContent =
                '.rn-loading-bar{position:fixed;top:0;left:0;height:3px;width:0;z-index:99999;' +
                'background:#6576ff;box-shadow:0 0 6px rgba(101,118,255,.6);opacity:0;pointer-events:none;' +
                'transition:opacity .2s}' +
                '.rn-loading-bar.rn-active{opacity:1;animation:rn-load 8s cubic-bezier(.1,.7,.3,1) forwards}' +
                '@keyframes rn-load{0%{width:0}20%{width:45%}100%{width:92%}}' +
                '@media (prefers-reduced-motion:reduce){.rn-loading-bar.rn-active{animation:none;width:100%}}';
            document.head.appendChild(css);
            bar = document.createElement('div');
            bar.className = 'rn-loading-bar';
            bar.setAttribute('aria-hidden', 'true');
            document.body.appendChild(bar);
        }
        bar.classList.remove('rn-active');
        void bar.offsetWidth;             // restart the animation
        bar.classList.add('rn-active');
    }
    function hideBar() {
        if (bar) bar.classList.remove('rn-active');
    }

    function releaseAll() {
        pendingHref = null;
        hideBar();
        document.querySelectorAll('form[data-rn-submitted]').forEach(function (f) {
            f.removeAttribute('data-rn-submitted');
            f.querySelectorAll('[data-rn-disabled]').forEach(function (b) {
                b.disabled = false;
                b.removeAttribute('data-rn-disabled');
            });
        });
    }
    function releaseLater() {
        clearTimeout(releaseTimer);
        releaseTimer = setTimeout(releaseAll, RELEASE_AFTER_MS);
    }

    // ---- Which tab is asking ---------------------------------------------
    // The server stops a page that's still loading when the same tab opens
    // another one (PageLoadMiddleware). Cookies are shared by every tab, so
    // just before this tab opens a page it puts its own id in the rn_tab
    // cookie. The id lives in window.name, which belongs to the tab and
    // survives moving between pages. A page opening in a NEW tab sends
    // "new", so it never stops the page loading in this one.
    var tabId = (function () {
        var m = /(?:^|;)rn-tab:([a-z0-9]{8,40})(?:;|$)/.exec(window.name || '');
        if (m) return m[1];
        var id = (Math.random().toString(36).slice(2, 10) + Date.now().toString(36)).toLowerCase();
        try { window.name = (window.name ? window.name + ';' : '') + 'rn-tab:' + id; } catch (e) { }
        return id;
    })();
    function tagRequest(value) {
        document.cookie = 'rn_tab=' + value + '; path=/; SameSite=Lax' +
            (location.protocol === 'https:' ? '; Secure' : '');
    }
    // Any click, key press (F5 included) or focus in this tab marks the next
    // page open as this tab's.
    ['pointerdown', 'keydown', 'focus'].forEach(function (type) {
        window.addEventListener(type, function () { tagRequest(tabId); }, true);
    });
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') tagRequest(tabId);
    });

    function opensHere(target) {
        return !target || target === '_self' || target === '_top' || target === '_parent';
    }

    // ---- Forms ----------------------------------------------------------
    // Capture phase: runs before the page's own submit handlers, so a
    // repeat is stopped before a confirm box can pop up a second time.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        var target = (e.submitter && e.submitter.getAttribute('formtarget')) || form.getAttribute('target');
        tagRequest(opensHere(target) ? tabId : 'new');
        if (form.hasAttribute('data-rn-no-guard')) return;
        if (form.hasAttribute('data-rn-submitted')) {
            e.preventDefault();
            e.stopImmediatePropagation();
        }
    }, true);

    // After every handler has run: if nobody cancelled it, the form is on
    // its way. Disabling now (not during the event) keeps the clicked
    // button's name/value in what's sent.
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        if (form.hasAttribute('data-rn-no-guard')) return;
        setTimeout(function () {
            if (e.defaultPrevented) return;
            var target = (e.submitter && e.submitter.getAttribute('formtarget')) || form.getAttribute('target');
            if (!opensHere(target)) return;   // opens in a new tab: this page stays usable
            form.setAttribute('data-rn-submitted', '');
            form.querySelectorAll('button[type=submit], button:not([type]), input[type=submit]').forEach(function (b) {
                if (!b.disabled) {
                    b.disabled = true;
                    b.setAttribute('data-rn-disabled', '');
                }
            });
            showBar();
            releaseLater();
        }, 0);
    });

    // ---- Links ----------------------------------------------------------
    function navigableLink(e) {
        if (e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return null;
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (!a || a.hasAttribute('download') || a.hasAttribute('data-rn-no-guard')) return null;
        if (!opensHere(a.getAttribute('target'))) return null;
        var raw = a.getAttribute('href');
        if (!raw || raw.charAt(0) === '#' || /^(javascript|mailto|tel):/i.test(raw)) return null;
        if (a.origin !== location.origin) return null;
        // Same page, only the #part differs: no request is made.
        if (a.pathname === location.pathname && a.search === location.search && a.hash) return null;
        return a;
    }

    function opensNewTab(e) {
        var a = e.target.closest ? e.target.closest('a[href]') : null;
        if (!a) return false;
        return e.button === 1 || e.ctrlKey || e.metaKey || e.shiftKey || !opensHere(a.getAttribute('target'));
    }
    document.addEventListener('auxclick', function (e) { if (e.button === 1 && opensNewTab(e)) tagRequest('new'); }, true);

    document.addEventListener('click', function (e) {
        if (opensNewTab(e)) { tagRequest('new'); return; }
        tagRequest(tabId);
        var a = navigableLink(e);
        if (!a) return;
        if (pendingHref !== null && a.href === pendingHref) {
            e.preventDefault();               // already loading this one
            e.stopImmediatePropagation();
        }
    }, true);

    document.addEventListener('click', function (e) {
        var a = navigableLink(e);
        if (!a) return;
        setTimeout(function () {
            if (e.defaultPrevented) return;   // a menu toggle, "show more", etc.
            pendingHref = a.href;
            showBar();
            releaseLater();
        }, 0);
    });

    // For rows and other elements that open a page from script:
    // onclick="Ranalo.go('/somewhere', event)". A repeat click is ignored; a
    // click on a link, button or box inside the row is left to that control
    // (otherwise the page opens twice); and a click that ends a text
    // selection (copying a phone number) doesn't open it.
    window.Ranalo = window.Ranalo || {};
    window.Ranalo.go = function (url, ev) {
        if (ev && ev.target && ev.target.closest &&
            ev.target.closest('a,button,input,select,textarea,label')) return;
        var sel = window.getSelection ? String(window.getSelection()) : '';
        if (sel.trim() !== '') return;
        var href = new URL(url, location.href).href;
        if (pendingHref === href) return;
        pendingHref = href;
        tagRequest(tabId);
        showBar();
        releaseLater();
        location.href = href;
    };

    // Back/Forward can show this page from the browser's memory with its
    // buttons still disabled and the bar still running.
    window.addEventListener('pageshow', function () {
        clearTimeout(releaseTimer);
        releaseAll();
    });
})();
