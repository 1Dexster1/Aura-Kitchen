import os

def patch_webgl():
    html_path = 'public/index.html'
    if not os.path.exists(html_path):
        print(f"Warning: {html_path} does not exist.")
        return

    with open(html_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update references to point to uncompressed files
    for ext in ['.framework.js.br', '.data.br', '.wasm.br', '.framework.js.gz', '.data.gz', '.wasm.gz']:
        raw = ext.replace('.br', '').replace('.gz', '')
        content = content.replace(ext, raw)

    # 2. Prevent blocking browser alert on errors
    content = content.replace('alert(message);', 'console.error("[Unity WebGL] Startup error:", message);')

    # 3. Add ScreenOrientation & Runtime Error Safety Wrapper
    patch = '''<script>
      // Comprehensive WebGL Error & ScreenOrientation Safety Wrapper
      (function() {
        var _origAlert = window.alert;
        window.alert = function(msg) {
          console.warn("[Unity WebGL Suppressed Alert]:", msg);
        };

        if (window.screen && window.screen.orientation) {
          try {
            var _origScreenAdd = window.screen.orientation.addEventListener;
            window.screen.orientation.addEventListener = function(t, fn, o) {
              return _origScreenAdd.call(this, t, function(e) {
                try { fn(e); } catch (err) { console.warn('Caught WebGL screen.orientation error:', err); }
              }, o);
            };
          } catch(e) {}
        }

        try {
          var _origWinAdd = window.addEventListener;
          window.addEventListener = function(type, listener, options) {
            if (type === 'orientationchange' || type === 'resize') {
              return _origWinAdd.call(this, type, function(evt) {
                try { listener(evt); } catch(err) { console.warn('Caught WebGL window.' + type + ' error:', err); }
              }, options);
            }
            return _origWinAdd.call(this, type, listener, options);
          };
        } catch(e) {}
      })();
'''
    if '<script>' in content:
        content = content.replace('<script>', patch, 1)

    with open(html_path, 'w', encoding='utf-8') as f:
        f.write(content)

    print("public/index.html successfully patched!")

if __name__ == '__main__':
    patch_webgl()

