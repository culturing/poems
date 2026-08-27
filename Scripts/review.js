/* Rating widget for the manual review pass. Emitted onto poem pages only by
   "dotnet run -- review", refuses to run anywhere but localhost, and writes nothing itself --
   Tools/review-server.py does, on 5501.

   Temporary: delete this file, Tools/ and the {{review}} line in Templates/content.html when
   the pass is done. */
(function () {
    'use strict';

    var LOCAL = ['localhost', '127.0.0.1', '[::1]'];
    if (LOCAL.indexOf(location.hostname) === -1) return;

    var API = 'http://127.0.0.1:5501';
    var url = location.pathname;
    var state = null;
    var busy = false;

    /* ---- chrome ---------------------------------------------------------------- */

    var css = [
        '#review{position:fixed;left:50%;bottom:1.25rem;transform:translateX(-50%);z-index:9999;',
        'display:flex;flex-wrap:nowrap;align-items:stretch;gap:.75rem;padding:.5rem .75rem;',
        'white-space:nowrap;',
        'background:#151515;border:1px solid #2b2b2b;border-radius:3px;',
        'font-family:Quattrocento,Georgia,serif;font-size:.8rem;color:#8a8a8a;',
        'box-shadow:0 2px 14px rgba(0,0,0,.5)}',
        '#review button{font:inherit;font-size:.8rem;cursor:pointer;color:#8a8a8a;',
        'background:transparent;border:1px solid #2b2b2b;border-radius:2px;',
        'padding:.15rem .55rem;min-width:2.4rem}',
        '#review button:hover{color:#f5f5f5;border-color:#8a8a8a}',
        '#review button:focus-visible{outline:2px solid #dd8b94;outline-offset:1px}',
        '#review button[aria-pressed="true"]{color:#0a0a0a;background:#f5f5f5;border-color:#f5f5f5}',
        '#review .r-count,#review .r-msg,#review .r-next{display:flex;align-items:center}',
        '#review .r-count{font-variant-numeric:tabular-nums}',
        '#review .r-buttons{display:flex;gap:.3rem}',
        '#review .r-sep{width:1px;align-self:stretch;background:#2b2b2b}',
        '#review .r-msg{min-width:7.5rem}',
        '#review .r-msg.warn{color:#dd8b94}',
        '#review a{color:#8a8a8a;text-decoration:none}',
        '#review a:hover{color:#f5f5f5}',
        '@media print{#review{display:none}}'
    ].join('');

    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);

    var bar = document.createElement('div');
    bar.id = 'review';
    bar.innerHTML =
        '<span class="r-count">&hellip;</span>' +
        '<span class="r-sep"></span>' +
        '<span class="r-buttons"></span>' +
        '<span class="r-sep"></span>' +
        '<a href="#" class="r-next" title="next unreviewed poem (u)">next unreviewed</a>' +
        '<span class="r-sep"></span>' +
        '<span class="r-msg"></span>';
    document.body.appendChild(bar);

    var elCount = bar.querySelector('.r-count');
    var elButtons = bar.querySelector('.r-buttons');
    var elNext = bar.querySelector('.r-next');
    var elMsg = bar.querySelector('.r-msg');

    var msgTimer = null;
    function say(text, warn) {
        elMsg.textContent = text;
        elMsg.className = 'r-msg' + (warn ? ' warn' : '');
        clearTimeout(msgTimer);
        if (text) msgTimer = setTimeout(function () { say(''); }, 2600);
    }

    function label(n) { return n === 0 ? '—' : new Array(n + 1).join('✱'); }

    function render() {
        if (!state) return;
        elCount.textContent = state.index + '/' + state.total +
            '  ·  ' + state.reviewedCount + ' done';
        elButtons.innerHTML = '';
        for (var n = 0; n <= state.max; n++) {
            var b = document.createElement('button');
            b.type = 'button';
            b.textContent = label(n);
            b.title = 'rate ' + n + '  (key ' + n + ')';
            b.setAttribute('aria-pressed', String(n === state.rating));
            b.addEventListener('click', (function (v) {
                return function () { rate(v); };
            })(n));
            elButtons.appendChild(b);
        }
        elNext.style.visibility = state.nextUnreviewed ? 'visible' : 'hidden';
        elNext.href = state.nextUnreviewed || '#';
    }

    /* ---- api ------------------------------------------------------------------- */

    function load() {
        fetch(API + '/api/poem?url=' + encodeURIComponent(url))
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (res) {
                if (!res.ok) throw new Error(res.j.error || 'request failed');
                state = res.j;
                render();
                if (state.reviewed) say('already reviewed');
            })
            .catch(function (err) {
                elCount.textContent = 'review server offline';
                elButtons.innerHTML = '';
                elNext.style.visibility = 'hidden';
                say(String(err.message || err), true);
            });
    }

    function rate(value) {
        if (busy || !state) return;
        busy = true;
        say('saving…');
        fetch(API + '/api/rating', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url: url, rating: value })
        })
            .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
            .then(function (res) {
                if (!res.ok) throw new Error(res.j.error || 'save failed');
                state = res.j;
                render();
                say(state.changed ? 'saved ' + label(state.rating) : 'kept ' + label(state.rating));
            })
            .catch(function (err) { say(String(err.message || err), true); })
            .then(function () { busy = false; });
    }

    /* ---- keyboard -------------------------------------------------------------- */

    function go(selector) {
        var a = document.querySelector(selector);
        if (a && a.getAttribute('href')) location.href = a.getAttribute('href');
    }

    document.addEventListener('keydown', function (e) {
        if (e.metaKey || e.ctrlKey || e.altKey) return;
        var tag = (e.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || e.target.isContentEditable) return;

        if (state && /^[0-9]$/.test(e.key)) {
            var n = Number(e.key);
            if (n <= state.max) { e.preventDefault(); rate(n); }
            return;
        }
        if (e.key === 'ArrowRight') { e.preventDefault(); go('#next'); }
        else if (e.key === 'ArrowLeft') { e.preventDefault(); go('#previous'); }
        else if (e.key === 'u' && state && state.nextUnreviewed) {
            e.preventDefault();
            location.href = state.nextUnreviewed;
        }
    });

    load();
})();
