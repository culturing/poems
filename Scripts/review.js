/* Rating and text editor for the manual review pass, emitted onto poem pages only by
   "dotnet run -- review", and carrying data-edit with "review edit". Writes nothing itself:
   Tools/review-server.cs does, on 5501 */
(function () {
    'use strict';

    var LOCAL = ['localhost', '127.0.0.1', '[::1]'];
    if (LOCAL.indexOf(location.hostname) === -1) return;

    var EDIT = document.currentScript.hasAttribute('data-edit');

    var API = 'http://127.0.0.1:5501';
    var url = location.pathname;
    var state = null;
    var busy = false;

    var css = [
        '#review{position:fixed;left:50%;bottom:1.25rem;transform:translateX(-50%);z-index:9999;',
        'display:flex;flex-wrap:nowrap;align-items:stretch;gap:.75rem;padding:.5rem .75rem;',
        'white-space:nowrap;',
        'background:#151515;border:1px solid #2b2b2b;border-radius:3px;',
        'font-family:Alegreya,Georgia,serif;font-size:.8rem;color:#a0a0a0;',
        'box-shadow:0 2px 14px rgba(0,0,0,.5)}',
        '#review button{font:inherit;font-size:.8rem;cursor:pointer;color:#a0a0a0;',
        'background:transparent;border:1px solid #2b2b2b;border-radius:2px;',
        'padding:.15rem .55rem;min-width:2.4rem}',
        '#review button:hover{color:#f5f5f5;border-color:#a0a0a0}',
        '#review button:focus-visible{outline:2px solid #dd8b94;outline-offset:1px}',
        '#review button[aria-pressed="true"]{color:#0a0a0a;background:#f5f5f5;border-color:#f5f5f5}',
        '#review .r-count,#review .r-msg,#review .r-next,#review .r-save,#review .r-rebuild,#review .r-dirty{display:flex;align-items:center}',
        '#review .r-count{font-variant-numeric:tabular-nums}',
        '#review .r-buttons{display:flex;gap:.3rem}',
        '#review .r-sep{width:1px;align-self:stretch;background:#2b2b2b}',
        '#review .r-msg{min-width:7.5rem}',
        '#review .r-msg.warn{color:#dd8b94}',
        '#review a{color:#a0a0a0;text-decoration:none}',
        '#review a:hover{color:#f5f5f5}',
        '#review.dragging{opacity:.85}',
        '#review .r-grip{align-self:center;cursor:grab;color:#a0a0a0;' +
        'letter-spacing:.1em;user-select:none;touch-action:none}',
        '#review.dragging .r-grip{cursor:grabbing}',
        '#review .r-dirty{color:#dd8b94}',
        '#review .r-dirty.unbuilt{color:#a0a0a0;font-style:italic}',
        '#review .r-save[aria-disabled="true"]{opacity:.4;pointer-events:none}',
        '#content [contenteditable]:focus{outline:1px dashed #2b2b2b;outline-offset:.35rem}',
        '#review [hidden]{display:none}',
        '@media print{#review{display:none}}'
    ].join('');

    var style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);

    var bar = document.createElement('div');
    bar.id = 'review';
    bar.innerHTML =
        '<span class="r-grip" title="drag to move">⋮⋮</span>' +
        '<span class="r-count">&hellip;</span>' +
        '<span class="r-sep"></span>' +
        '<span class="r-buttons"></span>' +
        '<span class="r-sep"></span>' +
        '<a href="#" class="r-next" title="next unreviewed poem (u)">next unreviewed</a>' +
        '<span class="r-sep"></span>' +
        '<span class="r-dirty" hidden></span>' +
        '<a href="#" class="r-save" title="save the text (ctrl+s)" aria-disabled="true">save</a>' +
        '<a href="#" class="r-rebuild" title="rebuild the site (b)">rebuild</a>' +
        '<span class="r-sep"></span>' +
        '<span class="r-msg"></span>';
    document.body.appendChild(bar);

    var elCount = bar.querySelector('.r-count');
    var elButtons = bar.querySelector('.r-buttons');
    var elNext = bar.querySelector('.r-next');
    var elMsg = bar.querySelector('.r-msg');
    var elSave = bar.querySelector('.r-save');
    var elDirty = bar.querySelector('.r-dirty');
    var elRebuild = bar.querySelector('.r-rebuild');

    var POSITION_KEY = 'review-bar-position';

    function place(left, top) {
        var box = bar.getBoundingClientRect();
        left = Math.min(Math.max(left, 4), Math.max(4, window.innerWidth - box.width - 4));
        top = Math.min(Math.max(top, 4), Math.max(4, window.innerHeight - box.height - 4));
        bar.style.left = left + 'px';
        bar.style.top = top + 'px';
        bar.style.bottom = 'auto';
        bar.style.transform = 'none';
        return { left: left, top: top };
    }

    function restore() {
        try {
            var saved = JSON.parse(localStorage.getItem(POSITION_KEY));
            if (saved) place(saved.left, saved.top);
        } catch (err) { /* no saved position */ }
    }

    var grip = bar.querySelector('.r-grip');
    var origin = null;

    grip.addEventListener('pointerdown', function (e) {
        var box = bar.getBoundingClientRect();
        origin = { x: e.clientX - box.left, y: e.clientY - box.top };
        grip.setPointerCapture(e.pointerId);
        bar.classList.add('dragging');
        e.preventDefault();
    });

    grip.addEventListener('pointermove', function (e) {
        if (!origin) return;
        place(e.clientX - origin.x, e.clientY - origin.y);
    });

    function endDrag(e) {
        if (!origin) return;
        origin = null;
        bar.classList.remove('dragging');
        if (grip.hasPointerCapture(e.pointerId)) grip.releasePointerCapture(e.pointerId);
        try {
            localStorage.setItem(POSITION_KEY, JSON.stringify({
                left: parseFloat(bar.style.left),
                top: parseFloat(bar.style.top)
            }));
        } catch (err) { /* storage unavailable */ }
    }

    grip.addEventListener('pointerup', endDrag);
    grip.addEventListener('pointercancel', endDrag);

    window.addEventListener('resize', function () {
        if (bar.style.top) place(parseFloat(bar.style.left), parseFloat(bar.style.top));
    });

    restore();

    var msgTimer = null;
    function say(text, warn, stay) {
        elMsg.textContent = text;
        elMsg.className = 'r-msg' + (warn ? ' warn' : '');
        clearTimeout(msgTimer);
        if (text && !stay) msgTimer = setTimeout(function () { say(''); }, 2600);
    }

    function call(path, body) {
        var options = body && {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        };
        return fetch(API + path, options)
            .then(function (r) {
                return r.json().then(function (j) {
                    if (!r.ok) throw new Error(j.error || 'request failed');
                    return j;
                });
            });
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

    function load() {
        call('/api/poem?url=' + encodeURIComponent(url))
            .then(function (j) {
                state = j;
                unbuilt = state.unbuilt;
                render();
                refresh();
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
        call('/api/rating', { url: url, rating: value })
            .then(function (j) {
                state = j;
                render();
                say(state.changed ? 'saved ' + label(state.rating) : 'kept ' + label(state.rating));
            })
            .catch(function (err) { say(String(err.message || err), true); })
            .then(function () { busy = false; });
    }

    /* The page is edited as rendered. Saving maps it back onto the source a line at a time,
       so every line left alone is written back exactly as it was */

    var OPAQUE = '\u0001';
    var content = document.getElementById('content');
    var title = content.querySelector('h1');
    var blocks = Array.prototype.filter.call(content.children, function (el) {
        return el !== title && !/^(dateline|url|tags)$/.test(el.className);
    });
    var source = null;
    var unbuilt = false;
    var building = false;

    // Lines of a paragraph, with emphasis written back as markdown
    function linesOf(el) {
        var lines = [''];
        (function walk(node) {
            Array.prototype.forEach.call(node.childNodes, function (child) {
                var name = child.nodeName;
                if (child.nodeType === Node.TEXT_NODE)
                    lines[lines.length - 1] += child.nodeValue.replace(/\n/g, '').replace(/ /g, ' ');
                else if (name === 'BR' || name === 'DIV') {
                    lines.push('');
                    walk(child);
                }
                else {
                    var mark = name === 'EM' || name === 'I' ? '*' : name === 'STRONG' || name === 'B' ? '**' : '';
                    lines[lines.length - 1] += mark;
                    walk(child);
                    lines[lines.length - 1] += mark;
                }
            });
        })(el);
        return lines.map(function (line) { return line.trimEnd(); });
    }

    // One token per line, '' for a stanza break, and OPAQUE for a block that cannot be edited
    function tidy(tokens) {
        return tokens.filter(function (token, i) {
            return token !== '' || (i > 0 && tokens[i - 1] !== '');
        }).join('\n').replace(/^\n+|\n+$/g, '').split('\n');
    }

    function pageTokens() {
        var tokens = [];
        blocks.forEach(function (el) {
            tokens.push('');
            if (el.nodeName === 'P') tokens.push.apply(tokens, linesOf(el));
            else tokens.push(OPAQUE + el.nodeName);
        });
        return tidy(tokens);
    }

    // The source as tokens matching pageTokens, each holding the exact lines it came from
    function parse(text) {
        var lines = text.split('\n');
        var body = lines.slice(2);
        var parts = [];
        for (var i = 0; i < body.length;) {
            var start = i;
            var token;
            if (body[i].trim() === '') {
                while (i < body.length && body[i].trim() === '') i++;
                token = '';
            }
            else if (body[i].charAt(0) === '>') {
                while (i < body.length && body[i].charAt(0) === '>') i++;
                if (i < body.length && /^(--|—) /.test(body[i])) i++; // The caption, as Markup.ExpandEpigraphs reads it
                token = OPAQUE + 'FIGURE';
            }
            else {
                token = body[i++];
            }
            parts.push({ token: token, lines: body.slice(start, i) });
        }

        var head = lines.slice(0, 2);
        var tail = [];
        while (parts.length && parts[0].token === '') head = head.concat(parts.shift().lines);
        while (parts.length && parts[parts.length - 1].token === '') tail = parts.pop().lines.concat(tail);

        return { title: lines[0], head: head, parts: parts, tail: tail, page: pageTokens(), heading: title.textContent };
    }

    function serialize() {
        var before = source.page;
        var after = pageTokens();
        var n = before.length;
        var m = after.length;
        var common = [];
        for (var i = 0; i <= n; i++) common.push(new Array(m + 1).fill(0));
        for (i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                common[i][j] = before[i] === after[j] ? common[i + 1][j + 1] + 1 : Math.max(common[i + 1][j], common[i][j + 1]);

        var body = [];
        for (i = 0, j = 0; i < n || j < m;) {
            if (i < n && j < m && before[i] === after[j]) { body = body.concat(source.parts[i].lines); i++; j++; }
            else if (j < m && (i === n || common[i][j + 1] >= common[i + 1][j])) body.push(after[j++]);
            else i++;
        }

        var heading = title.textContent.trim();
        var head = source.head.slice();
        if (heading !== source.heading.trim()) head[0] = heading;
        return head.concat(body, source.tail).join('\n');
    }

    function dirty() { return source !== null && serialize() !== source.text; }

    function refresh() {
        var unsaved = dirty();
        elSave.setAttribute('aria-disabled', String(!unsaved));
        elDirty.textContent = unsaved ? 'unsaved' : 'not rebuilt';
        elDirty.className = 'r-dirty' + (unsaved ? '' : ' unbuilt');
        elDirty.hidden = !unsaved && !unbuilt;
    }

    function begin(text) {
        source = parse(text);
        source.text = text;
        var opaque = function (tokens) { return tokens.filter(function (t) { return t.charAt(0) === OPAQUE; }).join(); };
        var tokens = source.parts.map(function (part) { return part.token; });
        if (tokens.length !== source.page.length || opaque(tokens) !== opaque(source.page)) {
            source = null;
            elSave.hidden = true;
            say('this page does not match its source, so it cannot be edited here', true);
            return;
        }

        [title].concat(blocks.filter(function (el) { return el.nodeName === 'P'; })).forEach(function (el) {
            el.contentEditable = 'true';
            el.spellcheck = true;
        });
        refresh();
    }

    function save() {
        if (busy || !dirty()) return;
        busy = true;
        var text = serialize();
        say('saving…', false, true);
        call('/api/text', { url: url, text: text })
            .then(function (j) {
                unbuilt = j.unbuilt;
                begin(text);
                // So the reload after a rebuild lands where a new title moved the poem
                if (j.url !== location.pathname) history.replaceState(null, '', j.url);
                say('saved');
            })
            .catch(function (err) { say(String(err.message || err), true); })
            .then(function () { busy = false; });
    }

    function rebuild() {
        if (building) return;
        building = true;
        say('rebuilding…', false, true);
        call('/api/build', {})
            .then(watchBuild)
            .catch(function (err) { building = false; say(String(err.message || err), true); });
    }

    function watchBuild() {
        call('/api/build')
            .then(function (j) {
                if (j.running) return setTimeout(watchBuild, 1500);
                building = false;
                if (j.failed) return say('build failed: Logs/review-build.log', true, true);
                if (!dirty()) return location.reload();
                unbuilt = false;
                refresh();
                say('rebuilt; reload once your edits are saved', false, true);
            })
            .catch(function () { setTimeout(watchBuild, 1500); }); // The server may be restarting
    }

    content.addEventListener('input', refresh);

    content.addEventListener('keydown', function (e) {
        if (!e.target.isContentEditable) return;
        if (e.key === 'Enter' && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            if (e.target !== title) document.execCommand('insertLineBreak'); // A paragraph's own Enter would split it into a new <p> or <div>
        }
    });

    // Rich text from elsewhere would bring markup the source cannot hold
    content.addEventListener('paste', function (e) {
        if (!e.target.isContentEditable) return;
        e.preventDefault();
        document.execCommand('insertText', false, e.clipboardData.getData('text/plain'));
    });

    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); save(); }
    });

    elSave.addEventListener('click', function (e) { e.preventDefault(); save(); });
    elRebuild.addEventListener('click', function (e) { e.preventDefault(); rebuild(); });

    window.addEventListener('beforeunload', function (e) {
        if (dirty()) { e.preventDefault(); e.returnValue = ''; }
    });

    if (EDIT)
        call('/api/text?url=' + encodeURIComponent(url))
            .then(function (j) { begin(j.text); })
            .catch(function () { elSave.hidden = true; });
    else
        elSave.hidden = true;

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
        else if (e.key === 'b') { e.preventDefault(); rebuild(); }
        else if (e.key === 'u' && state && state.nextUnreviewed) {
            e.preventDefault();
            location.href = state.nextUnreviewed;
        }
    });

    load();
})();
