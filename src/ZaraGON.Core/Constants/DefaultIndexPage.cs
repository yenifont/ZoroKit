namespace ZaraGON.Core.Constants;

public static class DefaultIndexPage
{
    public const string Content = """
        <?php
        $phpVersion = phpversion();
        $apacheVersion = function_exists('apache_get_version') ? apache_get_version() : ($_SERVER['SERVER_SOFTWARE'] ?? 'N/A');
        $os = PHP_OS_FAMILY;
        $docRoot = $_SERVER['DOCUMENT_ROOT'] ?? 'N/A';
        $serverName = $_SERVER['SERVER_NAME'] ?? 'localhost';
        $serverPort = $_SERVER['SERVER_PORT'] ?? '80';
        $protocol = (!empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off') ? 'https' : 'http';
        $extensions = [
            'curl' => extension_loaded('curl'),
            'mbstring' => extension_loaded('mbstring'),
            'openssl' => extension_loaded('openssl'),
            'pdo_mysql' => extension_loaded('pdo_mysql'),
            'gd' => extension_loaded('gd'),
            'zip' => extension_loaded('zip'),
            'intl' => extension_loaded('intl'),
            'fileinfo' => extension_loaded('fileinfo'),
            'xml' => extension_loaded('xml'),
            'json' => extension_loaded('json'),
        ];
        $dbStatus = false;
        $dbVersion = '';
        try {
            foreach (['3306', '3307'] as $p) {
                $conn = @new mysqli('127.0.0.1', 'root', '', '', (int)$p);
                if (!$conn->connect_error) {
                    $dbStatus = true;
                    $dbVersion = $conn->server_info;
                    $port = $p;
                    $conn->close();
                    break;
                }
            }
        } catch (Exception $e) {}
        $loadedExtCount = count(array_filter($extensions));
        $totalExtCount = count($extensions);
        ?>
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>ZaraGON - Local Development Environment</title>
        <style>
        *,*::before,*::after{box-sizing:border-box;margin:0;padding:0}
        :root{
          --bg:#0a0a0f;--surface:#12121a;--surface2:#1a1a26;--border:#2a2a3a;
          --text:#e8e8f0;--text2:#9898b0;--accent:#6366f1;--accent2:#818cf8;
          --accent-glow:rgba(99,102,241,.15);--green:#22c55e;--green-glow:rgba(34,197,94,.15);
          --red:#ef4444;--red-glow:rgba(239,68,68,.12);--orange:#f59e0b;--cyan:#06b6d4;
          --pink:#ec4899;--radius:16px;--radius-sm:10px;
        }
        body{
          font-family:'Inter','Segoe UI',-apple-system,system-ui,sans-serif;
          background:var(--bg);color:var(--text);min-height:100vh;overflow-x:hidden;
          -webkit-font-smoothing:antialiased;
        }
        .ambient{
          position:fixed;inset:0;pointer-events:none;z-index:0;
          background:
            radial-gradient(ellipse 80% 60% at 20% 10%,rgba(99,102,241,.08) 0%,transparent 60%),
            radial-gradient(ellipse 60% 50% at 80% 80%,rgba(236,72,153,.06) 0%,transparent 50%),
            radial-gradient(ellipse 50% 40% at 50% 50%,rgba(6,182,212,.04) 0%,transparent 50%);
        }
        .grain{
          position:fixed;inset:0;pointer-events:none;z-index:1;opacity:.03;
          background-image:url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E");
        }
        .wrapper{position:relative;z-index:2;max-width:1100px;margin:0 auto;padding:40px 24px 60px}
        .hero{text-align:center;padding:60px 0 50px}
        .logo-mark{
          display:inline-flex;align-items:center;justify-content:center;
          width:80px;height:80px;border-radius:24px;margin-bottom:28px;
          background:linear-gradient(135deg,var(--accent),var(--pink));
          box-shadow:0 0 60px rgba(99,102,241,.3),0 0 120px rgba(236,72,153,.15);
          animation:float 6s ease-in-out infinite;
        }
        .logo-mark svg{width:42px;height:42px;fill:#fff}
        @keyframes float{0%,100%{transform:translateY(0)}50%{transform:translateY(-8px)}}
        .hero h1{
          font-size:clamp(2.2rem,5vw,3.2rem);font-weight:800;letter-spacing:-.03em;line-height:1.1;
          background:linear-gradient(135deg,#fff 0%,var(--accent2) 50%,var(--pink) 100%);
          -webkit-background-clip:text;-webkit-text-fill-color:transparent;background-clip:text;
        }
        .hero .tagline{font-size:1.1rem;color:var(--text2);margin-top:14px}
        .hero .version-pill{
          display:inline-flex;align-items:center;gap:6px;margin-top:20px;padding:6px 16px;
          background:var(--surface2);border:1px solid var(--border);border-radius:100px;font-size:.8rem;color:var(--text2);
        }
        .hero .version-pill .dot{width:7px;height:7px;border-radius:50%;background:var(--green);box-shadow:0 0 8px var(--green)}
        .stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:16px;margin-bottom:36px}
        .stat-card{
          background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);
          padding:24px;transition:all .3s cubic-bezier(.4,0,.2,1);position:relative;overflow:hidden;
        }
        .stat-card::before{
          content:'';position:absolute;top:0;left:0;right:0;height:2px;
          background:linear-gradient(90deg,transparent,var(--accent),transparent);opacity:0;transition:opacity .3s;
        }
        .stat-card:hover{border-color:rgba(99,102,241,.3);transform:translateY(-2px);box-shadow:0 8px 32px rgba(0,0,0,.3)}
        .stat-card:hover::before{opacity:1}
        .stat-icon{width:44px;height:44px;border-radius:12px;display:flex;align-items:center;justify-content:center;margin-bottom:16px}
        .stat-icon.apache{background:var(--accent-glow);color:var(--accent2)}
        .stat-icon.php{background:rgba(139,92,246,.15);color:#a78bfa}
        .stat-icon.db{background:rgba(6,182,212,.12);color:var(--cyan)}
        .stat-icon.os{background:rgba(245,158,11,.12);color:var(--orange)}
        .stat-label{font-size:.78rem;color:var(--text2);text-transform:uppercase;letter-spacing:.08em;font-weight:500}
        .stat-value{font-size:1.4rem;font-weight:700;margin-top:4px;letter-spacing:-.02em}
        .stat-sub{font-size:.8rem;color:var(--text2);margin-top:4px}
        .stat-status{display:inline-flex;align-items:center;gap:5px;font-size:.78rem;margin-top:8px;padding:3px 10px;border-radius:100px;font-weight:500}
        .stat-status.ok{background:var(--green-glow);color:var(--green)}
        .stat-status.err{background:var(--red-glow);color:var(--red)}
        .section{margin-bottom:32px}
        .section-header{display:flex;align-items:center;gap:10px;margin-bottom:16px}
        .section-header h2{font-size:1.15rem;font-weight:700}
        .section-header .line{flex:1;height:1px;background:var(--border)}
        .ext-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:10px}
        .ext-item{display:flex;align-items:center;gap:10px;padding:12px 16px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius-sm);font-size:.88rem;font-weight:500;transition:all .2s}
        .ext-item:hover{border-color:rgba(99,102,241,.25)}
        .ext-dot{width:8px;height:8px;border-radius:50%;flex-shrink:0}
        .ext-dot.on{background:var(--green);box-shadow:0 0 8px var(--green)}
        .ext-dot.off{background:var(--red);opacity:.6}
        .ext-name{flex:1}
        .ext-badge{font-size:.65rem;padding:2px 7px;border-radius:100px;font-weight:600;text-transform:uppercase;letter-spacing:.04em}
        .ext-badge.on{background:var(--green-glow);color:var(--green)}
        .ext-badge.off{background:var(--red-glow);color:var(--red)}
        .links{display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:12px}
        .link-card{display:flex;align-items:center;gap:14px;padding:18px 20px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);text-decoration:none;color:var(--text);transition:all .3s cubic-bezier(.4,0,.2,1)}
        .link-card:hover{border-color:rgba(99,102,241,.35);transform:translateY(-2px);box-shadow:0 8px 24px rgba(0,0,0,.25)}
        .link-icon{width:42px;height:42px;border-radius:12px;display:flex;align-items:center;justify-content:center;flex-shrink:0}
        .link-icon.pma{background:rgba(245,158,11,.12);color:var(--orange)}
        .link-icon.info{background:rgba(99,102,241,.12);color:var(--accent2)}
        .link-icon.doc{background:rgba(34,197,94,.12);color:var(--green)}
        .link-title{font-weight:600;font-size:.92rem}
        .link-desc{font-size:.78rem;color:var(--text2);margin-top:2px}
        .link-arrow{margin-left:auto;color:var(--text2);font-size:1.1rem;transition:transform .2s}
        .link-card:hover .link-arrow{transform:translateX(3px);color:var(--accent2)}
        .info-table{width:100%;border-collapse:collapse;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);overflow:hidden}
        .info-table tr{border-bottom:1px solid var(--border)}
        .info-table tr:last-child{border-bottom:none}
        .info-table td{padding:12px 18px;font-size:.88rem}
        .info-table td:first-child{color:var(--text2);font-weight:500;width:220px;white-space:nowrap}
        .info-table td:last-child{font-family:'Cascadia Code','Fira Code',monospace;font-size:.82rem}
        .info-table tr:hover{background:var(--surface2)}
        .footer{text-align:center;padding:40px 0 0;color:var(--text2);font-size:.8rem}
        .footer .heart{color:var(--pink)}
        @media(max-width:640px){.wrapper{padding:20px 16px 40px}.hero{padding:40px 0 30px}.stats{grid-template-columns:1fr 1fr}.ext-grid{grid-template-columns:1fr 1fr}.links{grid-template-columns:1fr}}
        </style>
        </head>
        <body>
        <div class="ambient"></div>
        <div class="grain"></div>
        <div class="wrapper">
          <div class="hero">
            <div class="logo-mark">
              <svg viewBox="0 0 24 24"><path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5"/></svg>
            </div>
            <h1>ZaraGON</h1>
            <p class="tagline">Apache &middot; PHP &middot; MariaDB &mdash; Yerel Gelistirme Ortami</p>
            <div class="version-pill">
              <span class="dot"></span>
              Sunucu Aktif &mdash; <?= $serverName ?>:<?= $serverPort ?>
            </div>
          </div>
          <div class="stats">
            <div class="stat-card">
              <div class="stat-icon apache">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M12 2v20M2 12h20M4.93 4.93l14.14 14.14M19.07 4.93L4.93 19.07"/></svg>
              </div>
              <div class="stat-label">Apache</div>
              <div class="stat-value"><?= htmlspecialchars(preg_replace('/Apache\/?/i', '', explode(' ', $apacheVersion)[0] ?? $apacheVersion)) ?></div>
              <div class="stat-sub"><?= htmlspecialchars($apacheVersion) ?></div>
              <div class="stat-status ok"><span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--green)"></span> Calisiyor</div>
            </div>
            <div class="stat-card">
              <div class="stat-icon php">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M17 8h2a2 2 0 012 2v6a2 2 0 01-2 2h-2v-4h-2"/><path d="M7 8H5a2 2 0 00-2 2v6a2 2 0 002 2h2v-4h2"/><path d="M9 12h6"/></svg>
              </div>
              <div class="stat-label">PHP</div>
              <div class="stat-value"><?= $phpVersion ?></div>
              <div class="stat-sub"><?= php_sapi_name() ?> &middot; <?= PHP_INT_SIZE * 8 ?>-bit</div>
              <div class="stat-status ok"><span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--green)"></span> <?= $loadedExtCount ?>/<?= $totalExtCount ?> eklenti aktif</div>
            </div>
            <div class="stat-card">
              <div class="stat-icon db">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>
              </div>
              <div class="stat-label">MariaDB</div>
              <div class="stat-value"><?= $dbStatus ? htmlspecialchars($dbVersion) : 'N/A' ?></div>
              <div class="stat-sub"><?= $dbStatus ? "Port: $port" : 'Baglanti yok' ?></div>
              <div class="stat-status <?= $dbStatus ? 'ok' : 'err' ?>"><span class="dot" style="width:6px;height:6px;border-radius:50%;background:<?= $dbStatus ? 'var(--green)' : 'var(--red)' ?>"></span> <?= $dbStatus ? 'Calisiyor' : 'Durduruldu' ?></div>
            </div>
            <div class="stat-card">
              <div class="stat-icon os">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8M12 17v4"/></svg>
              </div>
              <div class="stat-label">Sistem</div>
              <div class="stat-value"><?= $os ?></div>
              <div class="stat-sub"><?= php_uname('m') ?> &middot; PHP <?= PHP_MAJOR_VERSION ?>.<?= PHP_MINOR_VERSION ?></div>
              <div class="stat-status ok"><span class="dot" style="width:6px;height:6px;border-radius:50%;background:var(--green)"></span> Hazir</div>
            </div>
          </div>
          <div class="section">
            <div class="section-header"><h2>Hizli Erisim</h2><div class="line"></div></div>
            <div class="links">
              <a href="/phpmyadmin" class="link-card">
                <div class="link-icon pma"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg></div>
                <div><div class="link-title">phpMyAdmin</div><div class="link-desc">Veritabani yonetim paneli</div></div>
                <span class="link-arrow">&rarr;</span>
              </a>
              <a href="/?phpinfo=1" class="link-card">
                <div class="link-icon info"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4M12 8h.01"/></svg></div>
                <div><div class="link-title">PHP Bilgisi</div><div class="link-desc">phpinfo() detaylari</div></div>
                <span class="link-arrow">&rarr;</span>
              </a>
              <a href="https://github.com/ZaraGON" class="link-card" target="_blank" rel="noopener">
                <div class="link-icon doc"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path d="M14 2v6h6M16 13H8M16 17H8M10 9H8"/></svg></div>
                <div><div class="link-title">Dokumantasyon</div><div class="link-desc">Kullanim kilavuzu</div></div>
                <span class="link-arrow">&rarr;</span>
              </a>
            </div>
          </div>
          <div class="section">
            <div class="section-header"><h2>PHP Eklentileri</h2><div class="line"></div></div>
            <div class="ext-grid">
              <?php foreach ($extensions as $name => $loaded): ?>
              <div class="ext-item">
                <span class="ext-dot <?= $loaded ? 'on' : 'off' ?>"></span>
                <span class="ext-name"><?= $name ?></span>
                <span class="ext-badge <?= $loaded ? 'on' : 'off' ?>"><?= $loaded ? 'Aktif' : 'Kapali' ?></span>
              </div>
              <?php endforeach; ?>
            </div>
          </div>
          <div class="section">
            <div class="section-header"><h2>Sunucu Detaylari</h2><div class="line"></div></div>
            <table class="info-table">
              <tr><td>Document Root</td><td><?= htmlspecialchars($docRoot) ?></td></tr>
              <tr><td>Sunucu Yazilimi</td><td><?= htmlspecialchars($apacheVersion) ?></td></tr>
              <tr><td>PHP SAPI</td><td><?= php_sapi_name() ?></td></tr>
              <tr><td>PHP Bellek Limiti</td><td><?= ini_get('memory_limit') ?></td></tr>
              <tr><td>Upload Maks. Boyut</td><td><?= ini_get('upload_max_filesize') ?></td></tr>
              <tr><td>Post Maks. Boyut</td><td><?= ini_get('post_max_size') ?></td></tr>
              <tr><td>Maks. Calisma Suresi</td><td><?= ini_get('max_execution_time') ?>s</td></tr>
              <tr><td>Zaman Dilimi</td><td><?= date_default_timezone_get() ?></td></tr>
              <tr><td>Sunucu IP</td><td><?= $_SERVER['SERVER_ADDR'] ?? '127.0.0.1' ?></td></tr>
            </table>
          </div>
          <div class="footer">
            <p><span class="heart">&hearts;</span> <strong>ZaraGON</strong> &mdash; Apache, PHP ve MariaDB yerel gelistirme ortami</p>
            <p style="margin-top:6px;opacity:.6"><?= date('Y') ?> &middot; v1.0.0</p>
          </div>
        </div>
        <?php if (isset($_GET['phpinfo'])) { echo '<div style="position:fixed;inset:0;z-index:9999;background:#fff;overflow:auto"><a href="/" style="position:fixed;top:16px;right:24px;z-index:10000;background:#6366f1;color:#fff;padding:8px 20px;border-radius:8px;text-decoration:none;font-family:sans-serif;font-weight:600;font-size:14px">&#x2715; Kapat</a>'; phpinfo(); echo '</div>'; } ?>
        </body>
        </html>
        """;
}
