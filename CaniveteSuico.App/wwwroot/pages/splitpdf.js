// pages/splitpdf.js — Split PDF (visual redesign)

(function () {
    /* ── DOM refs ─────────────────────────────────────────────────────── */
    const inputEl        = document.getElementById('split-input');
    const browseInBtn    = document.getElementById('split-browse-input');
    const pdfInfoBar     = document.getElementById('split-pdf-info');
    const infoName       = document.getElementById('split-info-name');
    const infoPages      = document.getElementById('split-info-pages');
    const infoSize       = document.getElementById('split-info-size');
    const modesWrap      = document.getElementById('split-modes-wrap');

    // Mode cards
    const modeCards      = document.querySelectorAll('.split-mode-card');

    // Mode panels
    const selectionPanel = document.getElementById('split-selection-panel');
    const partsPanel     = document.getElementById('split-parts-panel');
    const allPanel       = document.getElementById('split-all-panel');

    // Selection mode
    const chipsGrid      = document.getElementById('split-page-chips');
    const selectionCount = document.getElementById('split-selection-count');
    const selectAllBtn   = document.getElementById('split-select-all');
    const selectNoneBtn  = document.getElementById('split-select-none');

    // Parts mode
    const partsVal       = document.getElementById('split-parts-val');
    const partsMinus     = document.getElementById('split-parts-minus');
    const partsPlus      = document.getElementById('split-parts-plus');
    const partsHint      = document.getElementById('split-parts-hint');

    // All mode
    const allCount       = document.getElementById('split-all-count');

    // Output + action
    const outputEl       = document.getElementById('split-output');
    const browseOutBtn   = document.getElementById('split-browse-output');
    const convertBtn     = document.getElementById('split-convert-btn');
    const progressWrap   = document.getElementById('split-progress-wrap');
    const progressFill   = document.getElementById('split-progress-fill');
    const progressPct    = document.getElementById('split-progress-pct');
    const progressMsg    = document.getElementById('split-progress-msg');
    const statusEl       = document.getElementById('split-status');

    /* ── State ────────────────────────────────────────────────────────── */
    let currentMode   = 'selection';
    let totalPages    = 0;
    let selectedPages = new Set();

    /* ── Browse file ──────────────────────────────────────────────────── */
    browseInBtn.addEventListener('click', () =>
        browseFile('split-input', 'Selecionar PDF',
            'Documentos PDF|*.pdf|Todos|*.*', ''));

    // When a file is selected via the dialog, load its info
    document.addEventListener('cs-message', e => {
        const msg = e.detail;
        if (msg.type === 'DIALOG_RESULT' && msg.requestId === 'split-input' && msg.path) {
            loadPdfInfo(msg.path);
        }
    });

    function loadPdfInfo(path) {
        pdfInfoBar.style.display = 'none';
        modesWrap.style.display  = 'none';
        infoName.textContent = path.split(/[\\/]/).pop();
        callCSharp('PDF_INFO', { path });
    }

    /* ── Mode selection ───────────────────────────────────────────────── */
    modeCards.forEach(card => {
        card.addEventListener('click', () => {
            modeCards.forEach(c => c.classList.remove('active'));
            card.classList.add('active');
            currentMode = card.dataset.mode;
            showModePanel(currentMode);
        });
    });

    function showModePanel(mode) {
        selectionPanel.style.display = mode === 'selection' ? '' : 'none';
        partsPanel.style.display     = mode === 'parts'     ? '' : 'none';
        allPanel.style.display       = mode === 'all'       ? '' : 'none';

        if (mode === 'parts') updatePartsHint();
        if (mode === 'all')   allCount.textContent = totalPages;
    }

    /* ── Page chip grid ───────────────────────────────────────────────── */
    function buildChips(count) {
        chipsGrid.innerHTML = '';
        selectedPages.clear();

        for (let p = 1; p <= count; p++) {
            const chip = document.createElement('button');
            chip.className   = 'page-chip';
            chip.textContent = p;
            chip.dataset.page = p;
            chip.addEventListener('click', () => toggleChip(chip, p));
            chipsGrid.appendChild(chip);
        }
        updateSelectionCount();
    }

    function toggleChip(chip, page) {
        if (selectedPages.has(page)) {
            selectedPages.delete(page);
            chip.classList.remove('selected');
        } else {
            selectedPages.add(page);
            chip.classList.add('selected');
        }
        updateSelectionCount();
    }

    function updateSelectionCount() {
        const n = selectedPages.size;
        if (n === 0) {
            selectionCount.textContent = 'Clique nas páginas para selecioná-las';
        } else {
            selectionCount.textContent =
                `${n} página${n !== 1 ? 's' : ''} selecionada${n !== 1 ? 's' : ''} → 1 PDF gerado`;
        }
    }

    selectAllBtn.addEventListener('click', () => {
        chipsGrid.querySelectorAll('.page-chip').forEach(chip => {
            chip.classList.add('selected');
            selectedPages.add(parseInt(chip.dataset.page));
        });
        updateSelectionCount();
    });

    selectNoneBtn.addEventListener('click', () => {
        chipsGrid.querySelectorAll('.page-chip').forEach(chip => chip.classList.remove('selected'));
        selectedPages.clear();
        updateSelectionCount();
    });

    /* ── Parts stepper ────────────────────────────────────────────────── */
    partsMinus.addEventListener('click', () => {
        const v = parseInt(partsVal.value);
        if (v > 2) { partsVal.value = v - 1; updatePartsHint(); }
    });
    partsPlus.addEventListener('click', () => {
        const v = parseInt(partsVal.value);
        if (v < totalPages) { partsVal.value = v + 1; updatePartsHint(); }
    });
    partsVal.addEventListener('input', updatePartsHint);

    function updatePartsHint() {
        const n = Math.max(2, Math.min(parseInt(partsVal.value) || 2, totalPages));
        const ppp = Math.ceil(totalPages / n);
        partsHint.textContent =
            `≈ ${ppp} página${ppp !== 1 ? 's' : ''} por parte → ${n} PDFs gerados`;
    }

    /* ── Browse output dir ────────────────────────────────────────────── */
    browseOutBtn.addEventListener('click', () =>
        browseFolder('split-output', 'Escolher pasta de saída', outputEl.value.trim() || ''));

    /* ── Convert ──────────────────────────────────────────────────────── */
    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    function setProgress(pct, msg) {
        progressWrap.classList.add('visible');
        progressFill.style.width = pct + '%';
        progressPct.textContent  = pct + '%';
        if (msg) progressMsg.textContent = msg;
    }

    convertBtn.addEventListener('click', () => {
        const input = inputEl.value.trim();
        if (!input) { setStatus('error', 'Nenhum arquivo selecionado.'); return; }

        if (currentMode === 'selection' && selectedPages.size === 0) {
            setStatus('error', 'Selecione pelo menos uma página antes de dividir.');
            return;
        }

        progressWrap.classList.remove('visible');
        statusEl.className = 'status';
        convertBtn.disabled = true;
        setProgress(0, 'Iniciando…');

        const payload = {
            inputPath: input,
            mode:      currentMode,
            outputDir: outputEl.value.trim() || undefined,
        };

        if (currentMode === 'selection')
            payload.pages = [...selectedPages].sort((a, b) => a - b);

        if (currentMode === 'parts')
            payload.parts = Math.max(2, parseInt(partsVal.value) || 2);

        callCSharp('PDF_SPLIT', payload);
    });

    /* ── Message handler ──────────────────────────────────────────────── */
    document.addEventListener('cs-message', e => {
        const msg = e.detail;

        if (msg.action === 'PDF_INFO' && msg.type === 'PDF_INFO') {
            totalPages = msg.pageCount;

            infoName.textContent  = msg.fileName;
            infoPages.textContent = `${msg.pageCount} página${msg.pageCount !== 1 ? 's' : ''}`;
            infoSize.textContent  = msg.fileSizeFmt;

            pdfInfoBar.style.display = 'flex';
            modesWrap.style.display  = '';

            buildChips(msg.pageCount);
            allCount.textContent = msg.pageCount;
            partsVal.max = msg.pageCount;
            updatePartsHint();
            showModePanel(currentMode);
            return;
        }

        if (msg.action !== 'PDF_SPLIT') return;

        if (msg.type === 'PROGRESS') {
            setProgress(msg.percent ?? 0, msg.message);
        } else if (msg.type === 'SUCCESS') {
            setProgress(100, 'Concluído!');
            const fileWord = msg.fileCount === 1 ? 'arquivo gerado' : 'arquivos gerados';
            setStatus('success',
                `${msg.fileCount} ${fileWord} em:\n${msg.outputDir}`);
            showToast('success', `PDF dividido! ${msg.fileCount} ${fileWord}.`, 5000);
            convertBtn.disabled = false;
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Split falhou: ${msg.message}`);
            convertBtn.disabled = false;
        }
    });
})();
