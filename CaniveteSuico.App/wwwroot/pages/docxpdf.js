// pages/docxpdf.js — DOCX → PDF converter

(function () {
    const inputEl      = document.getElementById('docxpdf-input');
    const outputEl     = document.getElementById('docxpdf-output');
    const convertBtn   = document.getElementById('docxpdf-convert-btn');
    const browseInBtn  = document.getElementById('docxpdf-browse-input');
    const browseOutBtn = document.getElementById('docxpdf-browse-output');
    const statusEl     = document.getElementById('docxpdf-status');
    const stepsEl      = document.getElementById('docxpdf-steps');

    const STEPS = {
        reading:   document.getElementById('docxpdf-step-reading'),
        launching: document.getElementById('docxpdf-step-launching'),
        printing:  document.getElementById('docxpdf-step-printing'),
    };

    browseInBtn.addEventListener('click', () =>
        browseFile('docxpdf-input', 'Selecionar documento Word',
            'Documentos Word|*.docx|Todos os arquivos|*.*', ''));

    browseOutBtn.addEventListener('click', () =>
        browseFileSave('docxpdf-output', 'Salvar PDF como',
            'Documentos PDF|*.pdf|Todos os arquivos|*.*', ''));

    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    function resetSteps() {
        Object.values(STEPS).forEach(s => {
            if (s) s.className = 'step';
        });
    }

    function activateStep(name) {
        if (STEPS[name]) STEPS[name].className = 'step active';
    }

    function completeStep(name) {
        if (STEPS[name]) STEPS[name].className = 'step done';
    }

    convertBtn.addEventListener('click', () => {
        const inputPath = inputEl.value.trim();
        if (!inputPath) { setStatus('error', 'Por favor, informe o caminho do arquivo DOCX.'); return; }

        statusEl.className = 'status';
        convertBtn.disabled = true;
        stepsEl.style.display = 'flex';
        resetSteps();
        activateStep('reading');
        setStatus('info', 'Iniciando conversão…');

        callCSharp('DOCX_TO_PDF', {
            inputPath,
            outputPath: outputEl.value.trim() || undefined,
        });
    });

    document.addEventListener('cs-message', (e) => {
        const msg = e.detail;
        if (msg.action !== 'DOCX_TO_PDF') return;

        if (msg.type === 'PROGRESS') {
            setStatus('info', msg.message);

            switch (msg.status) {
                case 'reading':
                    activateStep('reading');
                    break;
                case 'launching':
                    completeStep('reading');
                    activateStep('launching');
                    break;
                case 'printing':
                    completeStep('launching');
                    activateStep('printing');
                    break;
            }
        } else if (msg.type === 'SUCCESS') {
            convertBtn.disabled = false;
            completeStep('printing');
            setStatus('success', `✅ PDF gerado com sucesso!\n${msg.outputPath}`);
            showToast('success', `DOCX → PDF concluído!\n${msg.outputPath}`, 5000);
        } else if (msg.type === 'ERROR') {
            convertBtn.disabled = false;
            resetSteps();
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Conversão falhou: ${msg.message}`);
        }
    });
})();
