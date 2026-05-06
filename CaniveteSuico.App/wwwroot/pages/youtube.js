// pages/youtube.js — YouTube Downloader + format/quality + playlist + history

(function () {
    /* ── DOM refs ────────────────────────────────────────────────────── */
    const urlInput     = document.getElementById('yt-url');
    const dirInput     = document.getElementById('yt-dir');
    const downloadBtn  = document.getElementById('yt-download-btn');
    const browseDirBtn = document.getElementById('yt-browse-dir');

    const statusEl     = document.getElementById('yt-status');
    const progressWrap = document.getElementById('yt-progress-wrap');
    const progressFill = document.getElementById('yt-progress-fill');
    const progressPct  = document.getElementById('yt-progress-pct');
    const logEl        = document.getElementById('yt-log');

    const playlistBar  = document.getElementById('yt-playlist-bar');
    const plTitle      = document.getElementById('yt-pl-title');
    const plCurrent    = document.getElementById('yt-pl-current');
    const plTotal      = document.getElementById('yt-pl-total');

    const itemLabel    = document.getElementById('yt-item-label');

    const historyWrap  = document.getElementById('yt-history-wrap');
    const historyList  = document.getElementById('yt-history-list');
    const historyClear = document.getElementById('yt-history-clear');

    /* ── Format / quality state ──────────────────────────────────────── */
    let currentFormat  = 'video';
    let currentQuality = 'best';

    // Format toggle
    document.querySelectorAll('#yt-format-ctrl .seg-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('#yt-format-ctrl .seg-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentFormat = btn.dataset.value;

            const qualGroup = document.getElementById('yt-quality-group');
            if (qualGroup) qualGroup.style.display = currentFormat === 'audio' ? 'none' : '';
        });
    });

    // Quality buttons
    document.querySelectorAll('.quality-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.quality-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentQuality = btn.dataset.value;
        });
    });

    /* ── Browse folder ───────────────────────────────────────────────── */
    browseDirBtn.addEventListener('click', () =>
        browseFolder('yt-dir', 'Escolher pasta de destino', dirInput.value.trim() || ''));

    /* ── UI helpers ─────────────────────────────────────────────────── */
    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    function setProgress(pct) {
        progressWrap.classList.add('visible');
        progressFill.style.width = pct + '%';
        progressPct.textContent = pct + '%';
    }

    function appendLog(msg) {
        logEl.classList.add('visible');
        const p = document.createElement('p');
        p.textContent = msg;
        logEl.appendChild(p);
        logEl.scrollTop = logEl.scrollHeight;
    }

    function resetUI() {
        progressWrap.classList.remove('visible');
        progressFill.style.width = '0%';
        progressPct.textContent = '0%';
        statusEl.className = 'status';
        logEl.className = 'log-area';
        logEl.innerHTML = '';
        playlistBar.classList.remove('visible');
        itemLabel.textContent = '';
        itemLabel.classList.remove('visible');
    }

    /* ── History panel ───────────────────────────────────────────────── */
    let downloads = [];

    function addToHistory(title, file, format) {
        downloads.unshift({
            title: title || file.split(/[\\/]/).pop(),
            file,
            format: format || 'mp4',
            completedAt: new Date(),
        });
        renderHistory();
    }

    function renderHistory() {
        if (downloads.length === 0) {
            historyWrap.classList.remove('visible');
            return;
        }
        historyWrap.classList.add('visible');
        historyList.innerHTML = '';

        downloads.forEach(item => {
            const fileName = item.file.split(/[\\/]/).pop();
            const time = item.completedAt.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
            const ext  = (item.format === 'audio' || fileName.toLowerCase().endsWith('.mp3')) ? 'mp3' : 'mp4';
            const icon = ext === 'mp3' ? '🎵' : '🎬';

            const card = document.createElement('div');
            card.className = 'dl-card';
            card.innerHTML = `
                <div class="dl-card-thumb">${icon}</div>
                <div class="dl-card-info">
                    <div class="dl-card-title" title="${escHtml(item.file)}">${escHtml(item.title)}</div>
                    <div class="dl-card-meta">
                        <span class="format-badge ${ext}">${ext.toUpperCase()}</span>
                        <span>${escHtml(fileName)}</span>
                        <span>·</span>
                        <span>${time}</span>
                    </div>
                </div>
                <div class="dl-card-actions">
                    <button class="btn-play" data-path="${escHtml(item.file)}" title="Abrir com player padrão">
                        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5">
                            <polygon points="5,3 13,8 5,13" fill="currentColor" stroke="none"/>
                        </svg>
                        Abrir
                    </button>
                    <button class="btn-folder" data-path="${escHtml(item.file)}" title="Mostrar na pasta">
                        <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5">
                            <path d="M1.5 5.5A1.5 1.5 0 013 4h3.086L7.5 5.5H13A1.5 1.5 0 0114.5 7v5A1.5 1.5 0 0113 13.5H3A1.5 1.5 0 011.5 12V5.5z"/>
                        </svg>
                    </button>
                </div>`;
            historyList.appendChild(card);
        });

        historyList.querySelectorAll('.btn-play').forEach(btn => {
            btn.addEventListener('click', () =>
                callCSharp('OPEN_FILE', { path: btn.dataset.path, reveal: false }));
        });
        historyList.querySelectorAll('.btn-folder').forEach(btn => {
            btn.addEventListener('click', () =>
                callCSharp('OPEN_FILE', { path: btn.dataset.path, reveal: true }));
        });
    }

    historyClear.addEventListener('click', () => {
        downloads = [];
        renderHistory();
    });

    function escHtml(str) {
        return (str ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    /* ── Download trigger ────────────────────────────────────────────── */
    downloadBtn.addEventListener('click', () => {
        const url = urlInput.value.trim();
        if (!url) { setStatus('error', 'Por favor, informe a URL do vídeo ou playlist.'); return; }

        resetUI();
        downloadBtn.disabled = true;
        setStatus('info', currentFormat === 'audio'
            ? 'Iniciando extração de áudio (MP3)…'
            : `Iniciando download (${currentQuality === 'best' ? 'melhor qualidade' : currentQuality})…`);

        callCSharp('YOUTUBE_DOWNLOAD', {
            url,
            outputDir: dirInput.value.trim() || undefined,
            format:    currentFormat,
            quality:   currentQuality,
        });
    });

    /* ── Message handler ─────────────────────────────────────────────── */
    document.addEventListener('cs-message', (e) => {
        const msg = e.detail;
        if (msg.action !== 'YOUTUBE_DOWNLOAD') return;

        switch (msg.type) {

            case 'PLAYLIST_INFO':
                playlistBar.classList.add('visible');
                plTitle.textContent = msg.playlistTitle || 'Playlist';
                plTotal.textContent = msg.total;
                plCurrent.textContent = '1';
                setStatus('info', `Playlist detectada: "${msg.playlistTitle}" (${msg.total} vídeos)`);
                break;

            case 'ITEM_START':
                plCurrent.textContent = msg.index;
                plTotal.textContent = msg.total;
                itemLabel.classList.add('visible');
                itemLabel.textContent = `⬇ [${msg.index}/${msg.total}] ${msg.title}`;
                setProgress(0);
                setStatus('info', msg.total > 1
                    ? `Baixando ${msg.index}/${msg.total}: ${msg.title}`
                    : `Baixando: ${msg.title}`);
                break;

            case 'PROGRESS':
                setProgress(msg.percent);
                break;

            case 'LOG':
                appendLog(msg.message);
                break;

            case 'ITEM_COMPLETE':
                setProgress(100);
                addToHistory(msg.title, msg.file, msg.format);
                itemLabel.textContent = `✓ [${msg.index}/${msg.total}] ${msg.title}`;
                break;

            case 'SUCCESS': {
                setProgress(100);
                const n = msg.total ?? 1;
                const label = msg.playlistTitle
                    ? `Playlist "${msg.playlistTitle}" concluída! ${n} vídeo${n !== 1 ? 's' : ''} baixado${n !== 1 ? 's' : ''}.`
                    : msg.format === 'audio'
                        ? 'Áudio extraído com sucesso!'
                        : 'Download concluído!';
                setStatus('success', label);
                showToast('success', label, 6000);
                downloadBtn.disabled = false;
                break;
            }

            case 'ERROR':
                setStatus('error', `Erro: ${msg.message}`);
                showToast('error', `Download falhou: ${msg.message}`);
                downloadBtn.disabled = false;
                break;
        }
    });
})();
