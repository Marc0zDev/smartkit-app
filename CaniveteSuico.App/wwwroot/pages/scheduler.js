// pages/scheduler.js — Download Scheduler

(function () {
    const urlEl         = document.getElementById('sched-url');
    const timeEl        = document.getElementById('sched-time');
    const dirEl         = document.getElementById('sched-dir');
    const browseDirBtn  = document.getElementById('sched-browse-dir');
    const addBtn        = document.getElementById('sched-add-btn');
    const clearBtn      = document.getElementById('sched-clear-btn');
    const statusEl      = document.getElementById('sched-status');
    const jobsWrap      = document.getElementById('sched-jobs-wrap');
    const jobsList      = document.getElementById('sched-jobs-list');

    let currentFormat  = 'video';
    let currentQuality = 'best';

    /* ── Set default datetime to 30 min from now ─────────────────────── */
    (function setDefaultTime() {
        const d = new Date(Date.now() + 30 * 60_000);
        d.setSeconds(0, 0);
        timeEl.value = d.toISOString().slice(0, 16);
    })();

    /* ── Format toggle ──────────────────────────────────────────────────── */
    document.querySelectorAll('#sched-format-ctrl .seg-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('#sched-format-ctrl .seg-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentFormat = btn.dataset.value;
            const qg = document.getElementById('sched-quality-group');
            if (qg) qg.style.display = currentFormat === 'audio' ? 'none' : '';
        });
    });

    document.querySelectorAll('#sched-quality-group .quality-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('#sched-quality-group .quality-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentQuality = btn.dataset.value;
        });
    });

    /* ── Browse ────────────────────────────────────────────────────────── */
    browseDirBtn.addEventListener('click', () =>
        browseFolder('sched-dir', 'Escolher pasta de destino', dirEl.value.trim() || ''));

    /* ── Status helper ──────────────────────────────────────────────────── */
    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    /* ── Add job ───────────────────────────────────────────────────────── */
    addBtn.addEventListener('click', () => {
        const url  = urlEl.value.trim();
        const time = timeEl.value;

        if (!url)  { setStatus('error', 'Informe a URL do vídeo ou playlist.'); return; }
        if (!time) { setStatus('error', 'Escolha a data e hora do download.'); return; }

        const scheduledTime = new Date(time).toISOString();
        const now = new Date();
        if (new Date(time) <= now) {
            setStatus('error', 'Escolha uma data/hora futura.'); return;
        }

        statusEl.className = 'status';
        addBtn.disabled = true;

        callCSharp('SCHEDULER', {
            command:       'ADD',
            url,
            format:        currentFormat,
            quality:       currentQuality,
            outputDir:     dirEl.value.trim() || undefined,
            scheduledTime,
        });
    });

    /* ── Clear finished jobs ────────────────────────────────────────────── */
    clearBtn.addEventListener('click', () => {
        callCSharp('SCHEDULER', { command: 'CLEAR' });
    });

    /* ── Render jobs ─────────────────────────────────────────────────────── */
    function renderJobs(jobs) {
        if (!jobs || jobs.length === 0) {
            jobsWrap.style.display = 'none';
            return;
        }
        jobsWrap.style.display = 'block';
        jobsList.innerHTML = '';

        jobs.forEach(job => {
            const card = document.createElement('div');
            card.className = 'dl-card';

            const scheduledDate = new Date(job.scheduledTime);
            const timeStr = scheduledDate.toLocaleString('pt-BR', {
                day: '2-digit', month: '2-digit',
                hour: '2-digit', minute: '2-digit',
            });

            const statusInfo = {
                pending:   { icon: '⏰', label: 'Aguardando', cls: '' },
                running:   { icon: '⬇', label: 'Baixando…', cls: 'running' },
                done:      { icon: '✓', label: 'Concluído', cls: 'done' },
                error:     { icon: '✕', label: 'Erro', cls: 'error' },
                cancelled: { icon: '—', label: 'Cancelado', cls: 'cancelled' },
            };
            const si = statusInfo[job.status] || statusInfo.pending;
            const fmt = job.format === 'audio' ? '🎵 MP3' : '🎬 MP4';

            card.innerHTML = `
                <div class="dl-card-thumb job-status-icon ${si.cls}">${si.icon}</div>
                <div class="dl-card-info">
                    <div class="dl-card-title" title="${escHtml(job.url)}">${escHtml(job.url)}</div>
                    <div class="dl-card-meta">
                        <span>${fmt}</span>
                        <span>·</span>
                        <span>${timeStr}</span>
                        <span>·</span>
                        <span class="job-status-text ${si.cls}">${si.label}${job.errorMessage ? ': ' + escHtml(job.errorMessage) : ''}</span>
                    </div>
                </div>
                <div class="dl-card-actions">
                    ${job.status === 'pending' ? `<button class="btn-play" data-cancel="${escHtml(job.id)}" title="Cancelar">✕</button>` : ''}
                </div>`;
            jobsList.appendChild(card);
        });

        jobsList.querySelectorAll('[data-cancel]').forEach(btn => {
            btn.addEventListener('click', () =>
                callCSharp('SCHEDULER', { command: 'CANCEL', jobId: btn.dataset.cancel }));
        });
    }

    /* ── Message handler ─────────────────────────────────────────────────── */
    document.addEventListener('cs-message', e => {
        const msg = e.detail;
        if (msg.action !== 'SCHEDULER') return;

        if (msg.type === 'SCHEDULER_JOB_ADDED') {
            setStatus('success', `Download agendado! ID: ${msg.jobId}`);
            showToast('success', 'Download agendado com sucesso!', 4000);
            addBtn.disabled = false;
            urlEl.value = '';
        } else if (msg.type === 'SCHEDULER_UPDATE') {
            renderJobs(msg.jobs);
        } else if (msg.type === 'SCHEDULER_EVENT') {
            const inner = msg.payload;
            if (inner?.type === 'SUCCESS') {
                showToast('success', `Download agendado concluído! (Job ${msg.jobId})`, 6000);
            } else if (inner?.type === 'ERROR') {
                showToast('error', `Download agendado falhou: ${inner.message} (Job ${msg.jobId})`);
            }
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            addBtn.disabled = false;
        }
    });

    function escHtml(str) {
        return (str ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    /* ── Request initial job list ─────────────────────────────────────── */
    document.querySelectorAll('.nav-item').forEach(btn => {
        if (btn.dataset.tab === 'scheduler') {
            btn.addEventListener('click', () =>
                callCSharp('SCHEDULER', { command: 'LIST' }));
        }
    });
})();
