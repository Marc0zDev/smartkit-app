// pages/video.js — Video Converter (FFmpeg)

(function () {
    const inputEl      = document.getElementById('video-input');
    const browseInBtn  = document.getElementById('video-browse-input');
    const formatEl     = document.getElementById('video-format');
    const outputEl     = document.getElementById('video-output');
    const browseOutBtn = document.getElementById('video-browse-output');
    const convertBtn   = document.getElementById('video-convert-btn');
    const progressWrap = document.getElementById('video-progress-wrap');
    const progressFill = document.getElementById('video-progress-fill');
    const progressPct  = document.getElementById('video-progress-pct');
    const progressMsg  = document.getElementById('video-progress-msg');
    const statusEl     = document.getElementById('video-status');
    const qualityGroup = document.getElementById('video-quality-group');

    let currentQuality = 'medium';

    /* ── Quality buttons ──────────────────────────────────────────────── */
    document.querySelectorAll('#tab-video .quality-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('#tab-video .quality-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentQuality = btn.dataset.value;
        });
    });

    /* ── Format change → hide quality for audio-only formats ─────────── */
    const audioFormats = ['mp3', 'aac', 'wav', 'flac'];
    formatEl.addEventListener('change', () => {
        const isAudio = audioFormats.includes(formatEl.value);
        qualityGroup.style.display = isAudio ? 'none' : '';
        outputEl.value = '';
    });

    /* ── Browse ────────────────────────────────────────────────────────── */
    const VIDEO_FILTER = 'Vídeos|*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.m4v;*.ts|Todos|*.*';

    browseInBtn.addEventListener('click', () =>
        browseFile('video-input', 'Selecionar arquivo de vídeo', VIDEO_FILTER, ''));

    browseOutBtn.addEventListener('click', () => {
        const ext = formatEl.value;
        browseFileSave('video-output', 'Salvar arquivo convertido como',
            `${ext.toUpperCase()}|*.${ext}|Todos|*.*`, '');
    });

    /* ── Helpers ───────────────────────────────────────────────────────── */
    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    function setProgress(pct, msg) {
        progressWrap.classList.add('visible');
        progressFill.style.width = pct + '%';
        progressPct.textContent = pct + '%';
        if (msg) progressMsg.textContent = msg;
    }

    /* ── Convert ───────────────────────────────────────────────────────── */
    convertBtn.addEventListener('click', () => {
        const input = inputEl.value.trim();
        if (!input) { setStatus('error', 'Selecione o arquivo de vídeo de entrada.'); return; }

        progressWrap.classList.remove('visible');
        statusEl.className = 'status';
        convertBtn.disabled = true;
        setProgress(0, 'Aguardando FFmpeg…');

        callCSharp('VIDEO_CONVERT', {
            inputPath:    input,
            outputFormat: formatEl.value,
            quality:      currentQuality,
            outputPath:   outputEl.value.trim() || undefined,
        });
    });

    document.addEventListener('cs-message', e => {
        const msg = e.detail;
        if (msg.action !== 'VIDEO_CONVERT') return;

        if (msg.type === 'PROGRESS') {
            setProgress(msg.percent ?? 0, msg.message);
        } else if (msg.type === 'SUCCESS') {
            setProgress(100, 'Concluído!');
            setStatus('success',
                `Convertido com sucesso! Tamanho: ${msg.sizeFormatted}\n${msg.outputPath}`);
            showToast('success', `Vídeo convertido! (${msg.sizeFormatted})`, 5000);
            convertBtn.disabled = false;
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Conversão falhou: ${msg.message}`);
            convertBtn.disabled = false;
        }
    });
})();
