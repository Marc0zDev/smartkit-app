// pages/mergepdf.js — Merge multiple PDFs

(function () {
    const fileListEl    = document.getElementById('merge-file-list');
    const addBtn        = document.getElementById('merge-add-btn');
    const outputEl      = document.getElementById('merge-output');
    const browseOutBtn  = document.getElementById('merge-browse-output');
    const convertBtn    = document.getElementById('merge-convert-btn');
    const progressWrap  = document.getElementById('merge-progress-wrap');
    const progressFill  = document.getElementById('merge-progress-fill');
    const progressPct   = document.getElementById('merge-progress-pct');
    const progressMsg   = document.getElementById('merge-progress-msg');
    const statusEl      = document.getElementById('merge-status');

    let files = [];   // array of file paths
    let browseCounter = 0;

    /* ── File list management ─────────────────────────────────────────── */
    function renderFiles() {
        fileListEl.innerHTML = '';

        if (files.length === 0) {
            fileListEl.innerHTML =
                '<div class="file-list-empty">Clique em "Adicionar PDF" para escolher os arquivos a mesclar.</div>';
        } else {
            files.forEach((path, idx) => {
                const name = path.split(/[\\/]/).pop();
                const dir  = path.substring(0, path.length - name.length).replace(/[\\/]$/, '');

                const row = document.createElement('div');
                row.className = 'file-list-item';
                row.draggable = true;
                row.dataset.idx = idx;
                row.innerHTML = `
                    <div class="file-list-order">${idx + 1}</div>
                    <div class="file-list-icon">📄</div>
                    <div class="file-list-info">
                        <div class="file-list-name" title="${escHtml(path)}">${escHtml(name)}</div>
                        <div class="file-list-dir">${escHtml(dir)}</div>
                    </div>
                    <div class="file-list-actions">
                        <button class="btn-ghost" data-action="up"   data-idx="${idx}" title="Subir"   ${idx === 0 ? 'disabled' : ''}>↑</button>
                        <button class="btn-ghost" data-action="down" data-idx="${idx}" title="Descer" ${idx === files.length - 1 ? 'disabled' : ''}>↓</button>
                        <button class="btn-ghost btn-danger-ghost" data-action="remove" data-idx="${idx}" title="Remover">✕</button>
                    </div>`;
                fileListEl.appendChild(row);
            });

            fileListEl.querySelectorAll('[data-action]').forEach(btn => {
                btn.addEventListener('click', () => {
                    const idx = parseInt(btn.dataset.idx);
                    if (btn.dataset.action === 'remove') {
                        files.splice(idx, 1);
                    } else if (btn.dataset.action === 'up' && idx > 0) {
                        [files[idx - 1], files[idx]] = [files[idx], files[idx - 1]];
                    } else if (btn.dataset.action === 'down' && idx < files.length - 1) {
                        [files[idx], files[idx + 1]] = [files[idx + 1], files[idx]];
                    }
                    renderFiles();
                });
            });
        }

        convertBtn.disabled = files.length < 2;
    }

    /* ── Browse ────────────────────────────────────────────────────────── */
    addBtn.addEventListener('click', () => {
        const tempId = `__merge_file_${++browseCounter}`;
        const tempInput = document.createElement('input');
        tempInput.id = tempId;
        tempInput.style.display = 'none';
        document.body.appendChild(tempInput);

        document.addEventListener('cs-message', function handler(e) {
            const msg = e.detail;
            if (msg.type === 'DIALOG_RESULT' && msg.requestId === tempId) {
                document.removeEventListener('cs-message', handler);
                tempInput.remove();
                if (msg.path) {
                    files.push(msg.path);
                    renderFiles();
                }
            }
        });

        browseFile(tempId, 'Selecionar PDF', 'Documentos PDF|*.pdf|Todos|*.*', '');
    });

    browseOutBtn.addEventListener('click', () =>
        browseFileSave('merge-output', 'Salvar PDF mesclado como',
            'Documentos PDF|*.pdf|Todos|*.*', ''));

    /* ── Convert ───────────────────────────────────────────────────────── */
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

    convertBtn.addEventListener('click', () => {
        if (files.length < 2) return;

        progressWrap.classList.remove('visible');
        statusEl.className = 'status';
        convertBtn.disabled = true;
        setProgress(0, 'Iniciando…');

        callCSharp('PDF_MERGE', {
            files,
            outputPath: outputEl.value.trim() || undefined,
        });
    });

    document.addEventListener('cs-message', e => {
        const msg = e.detail;
        if (msg.action !== 'PDF_MERGE') return;

        if (msg.type === 'PROGRESS') {
            setProgress(msg.percent ?? 0, msg.message);
        } else if (msg.type === 'SUCCESS') {
            setProgress(100, 'Concluído!');
            setStatus('success',
                `PDF mesclado com sucesso! ${msg.fileCount} arquivo(s) → ${msg.totalPages} página(s)\n${msg.outputPath}`);
            showToast('success', `PDF mesclado! ${msg.totalPages} páginas.`, 5000);
            convertBtn.disabled = false;
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Merge falhou: ${msg.message}`);
            convertBtn.disabled = files.length < 2;
        }
    });

    function escHtml(str) {
        return (str ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    renderFiles();
})();
