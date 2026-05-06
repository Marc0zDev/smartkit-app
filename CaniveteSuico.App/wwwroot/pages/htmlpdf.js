// pages/htmlpdf.js — HTML → PDF Converter (file/URL + raw HTML code)

(function () {
    const inputEl      = document.getElementById('hp-input');
    const codeEl       = document.getElementById('hp-code');
    const outputEl     = document.getElementById('hp-output');
    const convertBtn   = document.getElementById('hp-convert-btn');
    const browseInBtn  = document.getElementById('hp-browse-input');
    const browseOutBtn = document.getElementById('hp-browse-output');
    const statusEl     = document.getElementById('hp-status');
    const stepsEl      = document.getElementById('hp-steps');
    const navLabel     = document.getElementById('hp-step-nav-label');

    const fileGroup    = document.getElementById('hp-file-group');
    const codeGroup    = document.getElementById('hp-code-group');

    /* ── Source mode (file | code) ──────────────────────────────────── */
    let currentMode = 'file';

    document.querySelectorAll('#hp-mode-ctrl .seg-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('#hp-mode-ctrl .seg-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentMode = btn.dataset.value;

            const isCode = currentMode === 'code';
            fileGroup.style.display = isCode ? 'none' : '';
            codeGroup.style.display = isCode ? '' : 'none';

            if (navLabel) navLabel.textContent = isCode ? 'Renderizando HTML' : 'Carregando página';
        });
    });

    /* ── Browse buttons ─────────────────────────────────────────────── */
    browseInBtn.addEventListener('click', () =>
        browseFile('hp-input', 'Selecionar arquivo HTML',
            'Páginas HTML|*.html;*.htm|Todos os arquivos|*.*', ''));

    browseOutBtn.addEventListener('click', () =>
        browseFileSave('hp-output', 'Salvar PDF como',
            'Documentos PDF|*.pdf|Todos os arquivos|*.*', ''));

    /* ── Tab key support inside textarea ────────────────────────────── */
    codeEl.addEventListener('keydown', e => {
        if (e.key === 'Tab') {
            e.preventDefault();
            const start = codeEl.selectionStart;
            const end   = codeEl.selectionEnd;
            codeEl.value = codeEl.value.slice(0, start) + '  ' + codeEl.value.slice(end);
            codeEl.selectionStart = codeEl.selectionEnd = start + 2;
        }
    });

    /* ── UI helpers ─────────────────────────────────────────────────── */
    const STEPS = ['launching', 'navigating', 'printing'];

    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    function setStep(activeStatus) {
        stepsEl.classList.add('visible');
        stepsEl.querySelectorAll('.step').forEach(el => {
            const s = el.dataset.step;
            const idx = STEPS.indexOf(s);
            const activeIdx = STEPS.indexOf(activeStatus);
            el.className = 'step ' + (idx < activeIdx ? 'done' : idx === activeIdx ? 'active' : '');
        });
    }

    function resetUI() {
        statusEl.className = 'status';
        stepsEl.classList.remove('visible');
        stepsEl.querySelectorAll('.step').forEach(el => el.className = 'step');
    }

    /* ── Convert trigger ────────────────────────────────────────────── */
    convertBtn.addEventListener('click', () => {
        if (currentMode === 'code') {
            const code = codeEl.value.trim();
            if (!code) { setStatus('error', 'Cole o código HTML antes de converter.'); return; }

            resetUI();
            convertBtn.disabled = true;
            setStatus('info', 'Aguardando Edge headless…');

            callCSharp('HTML_TO_PDF', {
                inputType: 'code',
                htmlCode: code,
                outputPath: outputEl.value.trim() || undefined,
            });
        } else {
            const input = inputEl.value.trim();
            if (!input) { setStatus('error', 'Informe o arquivo HTML ou URL de entrada.'); return; }

            resetUI();
            convertBtn.disabled = true;
            setStatus('info', 'Aguardando Edge headless…');

            callCSharp('HTML_TO_PDF', {
                inputType: 'file',
                input,
                outputPath: outputEl.value.trim() || undefined,
            });
        }
    });

    /* ── Message handler ─────────────────────────────────────────────── */
    document.addEventListener('cs-message', (e) => {
        const msg = e.detail;
        if (msg.action !== 'HTML_TO_PDF') return;

        if (msg.type === 'PROGRESS') {
            setStep(msg.status);
            setStatus('info', msg.message);

        } else if (msg.type === 'SUCCESS') {
            stepsEl.querySelectorAll('.step').forEach(el => el.className = 'step done');
            setStatus('success', `PDF gerado com sucesso! Salvo em: ${msg.outputPath}`);
            showToast('success', `HTML → PDF concluído!\n${msg.outputPath}`, 6000);
            convertBtn.disabled = false;

        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Conversão falhou: ${msg.message}`);
            convertBtn.disabled = false;
        }
    });
})();
