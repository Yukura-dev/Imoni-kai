/*
 * support.js — スタンドアロン版 .dc.html ランタイム(モック閲覧用)
 * ------------------------------------------------------------------
 * 各 *.dc.html は <x-dc> 内のテンプレート + <script data-dc-script> の
 * `class Component extends DCLogic { renderVals() {...} }` で書かれている。
 * このファイルはそれをブラウザ単体で描画・操作できるようにする最小実装。
 *
 * 対応している記法:
 *   {{a.b}}                 … renderVals() の値の参照(式は不可)
 *   attr="{{x}}"            … 値そのもの(関数ならイベント、真偽値なら disabled 等)
 *   attr="a {{x}} b"        … 文字列に埋め込み
 *   onClick / onChange      … イベント(input の onChange は input イベント)
 *   <sc-if value="{{c}}">   … 条件表示
 *   <sc-for list="{{xs}}" as="it"> … 繰り返し({{$index}} も使える)
 *   <dc-import name="X" foo="{{1}}"> … X.dc.html を子として埋め込む(props は kebab→camel)
 *   <helmet>                … 中の <link>/<style> を <head> に入れる
 * URL の ?demo=true&fail=true などはトップレベルの props になる。
 * 使い方: このフォルダで `python3 -m http.server 8000` → http://localhost:8000/index.html
 * (dc-import は fetch を使うので file:// では動かない)
 */
(function () {
  'use strict';
  var hideStyle = document.createElement('style');
  hideStyle.textContent = 'x-dc{display:block}x-dc:not([data-ready]){visibility:hidden}';
  document.head.appendChild(hideStyle);

  var WHOLE = /^\s*\{\{([^}]+)\}\}\s*$/;
  var ANY = /\{\{([^}]+)\}\}/g;
  var BOOL_PROPS = { disabled: 1, checked: 1, selected: 1, readonly: 1, hidden: 1 };

  function lookup(scope, expr) {
    expr = expr.trim();
    if (expr === 'true') return true;
    if (expr === 'false') return false;
    if (expr === 'null') return null;
    if (/^-?\d+(\.\d+)?$/.test(expr)) return Number(expr);
    if (/^'.*'$/.test(expr)) return expr.slice(1, -1);
    var v = scope;
    var parts = expr.split('.');
    for (var i = 0; i < parts.length; i++) { if (v == null) return undefined; v = v[parts[i]]; }
    return v;
  }
  function interp(str, scope) {
    return str.replace(ANY, function (_, e) { var v = lookup(scope, e); return v == null ? '' : String(v); });
  }
  function camel(s) { return s.replace(/-([a-z])/g, function (_, c) { return c.toUpperCase(); }); }

  function DCLogic(props) { this.props = props || {}; this.state = {}; }
  DCLogic.prototype.setState = function (u) {
    var patch = typeof u === 'function' ? u(this.state, this.props) : u;
    this.state = Object.assign({}, this.state, patch);
    if (this.__schedule) this.__schedule();
  };
  DCLogic.prototype.forceUpdate = function () { if (this.__schedule) this.__schedule(); };
  window.DCLogic = DCLogic;

  var headSeen = {};
  function installHelmet(tpl) {
    var helmets = tpl.querySelectorAll ? tpl.querySelectorAll('helmet') : [];
    Array.prototype.forEach.call(helmets, function (h) {
      Array.prototype.forEach.call(h.children, function (c) {
        var key = c.outerHTML;
        if (headSeen[key]) return;
        headSeen[key] = 1;
        document.head.appendChild(document.importNode(c, true));
      });
      h.parentNode.removeChild(h);
    });
  }
  function compile(scriptEl) {
    var src = scriptEl.textContent;
    /* eslint-disable no-new-func */
    return new Function('DCLogic', src + '\n;return Component;')(DCLogic);
  }

  // ---------- テンプレート → DOM ----------
  function setHandler(el, evt, fn) {
    if (!el.__h) el.__h = {};
    if (!el.__bound) el.__bound = {};
    el.__h[evt] = fn;
    if (!el.__bound[evt]) {
      el.__bound[evt] = 1;
      el.addEventListener(evt, function (e) { var f = el.__h && el.__h[evt]; if (typeof f === 'function') f(e); });
    }
  }
  function applyAttr(el, name, raw, scope) {
    var lname = name.toLowerCase();
    if (lname.indexOf('hint-') === 0) { el.removeAttribute(name); return; }
    var m = raw.match(WHOLE);
    var val = m ? lookup(scope, m[1]) : (raw.indexOf('{{') >= 0 ? interp(raw, scope) : raw);
    if (lname.indexOf('on') === 0 && lname.length > 2) {
      el.removeAttribute(name);
      var evt = lname.slice(2);
      if (evt === 'change' && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) evt = 'input';
      el.__pendingH = el.__pendingH || {};
      el.__pendingH[evt] = val;
      return;
    }
    if (BOOL_PROPS[lname]) {
      var on = !(val === false || val === 'false' || val == null) && (m ? !!val : true);
      if (on) el.setAttribute(name, ''); else el.removeAttribute(name);
      el.__props = el.__props || {}; el.__props[lname] = on;
      return;
    }
    if (lname === 'value' && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) {
      el.__props = el.__props || {}; el.__props.value = val == null ? '' : String(val);
    }
    if (raw.indexOf('{{') >= 0) el.setAttribute(name, val == null ? '' : String(val));
  }
  function renderNodes(nodes, scope, out, host) {
    for (var i = 0; i < nodes.length; i++) renderNode(nodes[i], scope, out, host);
  }
  function renderNode(t, scope, out, host) {
    if (t.nodeType === 3) {
      var tx = t.nodeValue;
      out.appendChild(document.createTextNode(tx.indexOf('{{') >= 0 ? interp(tx, scope) : tx));
      return;
    }
    if (t.nodeType !== 1) return;
    var tag = t.localName;
    if (tag === 'sc-if') {
      var cv = t.getAttribute('value') || '';
      var cm = cv.match(WHOLE);
      if (cm ? lookup(scope, cm[1]) : cv) renderNodes(t.childNodes, scope, out, host);
      return;
    }
    if (tag === 'sc-for') {
      var lv = t.getAttribute('list') || '';
      var lm = lv.match(WHOLE);
      var list = lm ? lookup(scope, lm[1]) : [];
      var as = t.getAttribute('as') || 'item';
      (list || []).forEach(function (item, idx) {
        var s = Object.create(scope); s[as] = item; s.$index = idx;
        renderNodes(t.childNodes, s, out, host);
      });
      return;
    }
    if (tag === 'dc-import') {
      var ph = document.createElement('div');
      var props = {};
      Array.prototype.forEach.call(t.attributes, function (a) {
        if (a.name === 'name' || a.name.indexOf('hint-') === 0) return;
        var mm = a.value.match(WHOLE);
        props[camel(a.name)] = mm ? lookup(scope, mm[1]) : interp(a.value, scope);
      });
      var hs = (t.getAttribute('hint-size') || '').split(',');
      if (hs[0]) ph.style.width = hs[0].trim();
      if (hs[1]) ph.style.height = hs[1].trim();
      host.__importCount = (host.__importCount || 0) + 1;
      ph.__importKey = t.getAttribute('name') + '#' + host.__importCount;
      ph.__importName = t.getAttribute('name');
      ph.__importProps = props;
      out.appendChild(ph);
      return;
    }
    var el = document.importNode(t, false);
    Array.prototype.slice.call(t.attributes).forEach(function (a) { applyAttr(el, a.name, a.value, scope); });
    if (el.__pendingH) { var ph2 = el.__pendingH; el.__pendingH = null; Object.keys(ph2).forEach(function (k) { setHandler(el, k, ph2[k]); }); }
    if (el.__props && el.__props.value !== undefined) el.value = el.__props.value;
    renderNodes(t.childNodes, scope, el, host);
    out.appendChild(el);
  }

  // ---------- 差分更新(CSSアニメーションや入力フォーカスを保つ) ----------
  function morph(o, n) {
    if (o === n) return;
    if (o.nodeType !== n.nodeType || o.nodeName !== n.nodeName || o.namespaceURI !== n.namespaceURI) { o.parentNode.replaceChild(n, o); return; }
    if (o.nodeType === 3) { if (o.nodeValue !== n.nodeValue) o.nodeValue = n.nodeValue; return; }
    if (n.__importKey || o.__importKey) {
      if (o.__importKey === n.__importKey) { o.__importProps = n.__importProps; if (o.__child) { o.__child.props = Object.assign({}, o.__child.props, n.__importProps); o.__child.__schedule(); } return; }
      o.parentNode.replaceChild(n, o); return;
    }
    var i, a;
    for (i = o.attributes.length - 1; i >= 0; i--) { a = o.attributes[i]; if (!n.hasAttribute(a.name)) o.removeAttribute(a.name); }
    for (i = 0; i < n.attributes.length; i++) { a = n.attributes[i]; if (o.getAttribute(a.name) !== a.value) o.setAttribute(a.name, a.value); }
    if (n.__h) { Object.keys(n.__h).forEach(function (k) { setHandler(o, k, n.__h[k]); }); }
    if (o.__h) { Object.keys(o.__h).forEach(function (k) { if (!n.__h || !(k in n.__h)) o.__h[k] = null; }); }
    if (n.__props) {
      if (n.__props.value !== undefined && o.value !== n.__props.value) o.value = n.__props.value;
      ['disabled', 'checked', 'selected'].forEach(function (p) { if (p in n.__props) o[p] = n.__props[p]; });
    }
    morphChildren(o, n);
  }
  function morphChildren(o, n) {
    var i, oc = Array.prototype.slice.call(o.childNodes), nc = Array.prototype.slice.call(n.childNodes);
    for (i = 0; i < nc.length; i++) { if (i < oc.length) morph(oc[i], nc[i]); else o.appendChild(nc[i]); }
    for (i = nc.length; i < oc.length; i++) o.removeChild(oc[i]);
  }

  // ---------- コンポーネント ----------
  var defCache = {};
  function loadDef(name) {
    if (!defCache[name]) {
      defCache[name] = fetch(name + '.dc.html').then(function (r) { return r.text(); }).then(function (html) {
        var doc = new DOMParser().parseFromString(html, 'text/html');
        return makeDef(doc.querySelector('x-dc'), doc.querySelector('script[data-dc-script]'));
      });
    }
    return defCache[name];
  }
  function makeDef(xdc, script) {
    var tpl = document.createElement('div');
    while (xdc.firstChild) tpl.appendChild(document.importNode(xdc.firstChild, true)), xdc.removeChild(xdc.firstChild);
    installHelmet(tpl);
    return { tpl: tpl, Cls: compile(script) };
  }
  function mount(def, host, props) {
    var inst = new def.Cls(props || {});
    if (!inst.props) inst.props = props || {};
    if (!inst.state) inst.state = {};
    var pending = false, mounted = false;
    function render() {
      pending = false;
      var vals;
      try { vals = inst.renderVals ? inst.renderVals() : {}; } catch (e) { console.error(e); return; }
      host.__importCount = 0;
      var frag = document.createElement('div');
      renderNodes(def.tpl.childNodes, vals || {}, frag, host);
      if (!mounted) { while (host.firstChild) host.removeChild(host.firstChild); while (frag.firstChild) host.appendChild(frag.firstChild); }
      else morphChildren(host, frag);
      mountImports(host);
      if (!mounted) { mounted = true; host.setAttribute && host.setAttribute('data-ready', ''); if (inst.componentDidMount) { try { inst.componentDidMount(); } catch (e) { console.error(e); } } }
    }
    inst.__schedule = function () { if (pending) return; pending = true; Promise.resolve().then(render); };
    render();
    window.addEventListener('pagehide', function () { if (inst.componentWillUnmount) inst.componentWillUnmount(); });
    return inst;
  }
  function mountImports(root) {
    var walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
    var list = [];
    while (walker.nextNode()) { if (walker.currentNode.__importKey && !walker.currentNode.__mounting) list.push(walker.currentNode); }
    list.forEach(function (ph) {
      ph.__mounting = true;
      loadDef(ph.__importName).then(function (def) { ph.__child = mount(def, ph, Object.assign({}, ph.__importProps)); })
        .catch(function (e) { ph.textContent = ph.__importName + ' を読み込めません(http サーバーで開いてください)'; console.error(e); });
    });
  }
  function urlProps() {
    var p = {};
    new URLSearchParams(location.search).forEach(function (v, k) {
      p[k] = v === 'true' ? true : v === 'false' ? false : (/^-?\d+(\.\d+)?$/.test(v) ? Number(v) : v);
    });
    return p;
  }
  function boot() {
    var xdc = document.querySelector('x-dc');
    var script = document.querySelector('script[data-dc-script]');
    if (!xdc || !script) return;
    var def = makeDef(xdc, script);
    mount(def, xdc, urlProps());
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot); else boot();
})();
