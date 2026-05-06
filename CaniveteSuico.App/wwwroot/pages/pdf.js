// pages/pdf.js — PDF → Documento converter (DOCX / TXT / HTML / MD)

(function () {
    const inputEl      = document.getElementById('pdf-input');
    const formatEl     = document.getElementById('pdf-format');
    const outputEl     = document.getElementById('pdf-output');
    const convertBtn   = document.getElementById('pdf-convert-btn');
    const browseInBtn  = document.getElementById('pdf-browse-input');
    const browseOutBtn = document.getElementById('pdf-browse-output');
    const statusEl     = document.getElementById('pdf-status');

    /* ── Filter map per format ────────────────────────────────────────── */
    const FORMAT_META = {
        docx: { label: 'Word',     filter: 'Documentos Word|*.docx|Todos|*.*',   ext: '.docx', icon: '📄' },
        txt:  { label: 'TXT',      filter: 'Texto simples|*.txt|Todos|*.*',       ext: '.txt',  icon: '📃' },
        html: { label: 'HTML',     filter: 'Páginas HTML|*.html|Todos|*.*',       ext: '.html', icon: '🌐' },
        md:   { label: 'Markdown', filter: 'Markdown|*.md;*.markdown|Todos|*.*', ext: '.md',   icon: '✍' },
    };

    function currentMeta() {
        return FORMAT_META[formatEl.value] ?? FORMAT_META.docx;
    }

    /* ── When format changes: clear output path (ext changed) ─────────── */
    formatEl.addEventListener('change', () => {
        outputEl.value = '';
        outputEl.placeholder = `Mesma pasta do PDF (padrão) — salva como ${currentMeta().ext}`;
    });

    browseInBtn.addEventListener('click', () =>
        browseFile('pdf-input', 'Selecionar arquivo PDF',
            'Documentos PDF|*.pdf|Todos os arquivos|*.*', ''));

    browseOutBtn.addEventListener('click', () => {
        const m = currentMeta();
        browseFileSave('pdf-output', `Salvar como ${m.label}`, m.filter, '');
    });

    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    convertBtn.addEventListener('click', () => {
        const inputPath = inputEl.value.trim();
        if (!inputPath) { setStatus('error', 'Por favor, informe o caminho do arquivo PDF.'); return; }

        statusEl.className = 'status';
        convertBtn.disabled = true;
        const m = currentMeta();
        setStatus('info', `Convertendo PDF para ${m.label}…`);

        callCSharp('PDF_CONVERT', {
            inputPath,
            outputFormat: formatEl.value,
            outputPath: outputEl.value.trim() || undefined,
        });
    });

    document.addEventListener('cs-message', (e) => {
        const msg = e.detail;
        if (msg.action !== 'PDF_CONVERT') return;

        convertBtn.disabled = false;

        if (msg.type === 'SUCCESS') {
            const m = currentMeta();
            setStatus('success',
                `${m.icon} Convertido! ${msg.pageCount} página(s) → ${msg.outputPath}`);
            showToast('success', `PDF → ${m.label} concluído!\n${msg.outputPath}`, 5000);
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Conversão falhou: ${msg.message}`);
        }
    });
})();
