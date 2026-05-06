// pages/compresspdf.js — Compress PDF

(function () {
    const inputEl      = document.getElementById('compress-input');
    const browseInBtn  = document.getElementById('compress-browse-input');
    const outputEl     = document.getElementById('compress-output');
    const browseOutBtn = document.getElementById('compress-browse-output');
    const convertBtn   = document.getElementById('compress-convert-btn');
    const statusEl     = document.getElementById('compress-status');

    browseInBtn.addEventListener('click', () =>
        browseFile('compress-input', 'Selecionar PDF',
            'Documentos PDF|*.pdf|Todos|*.*', ''));

    browseOutBtn.addEventListener('click', () =>
        browseFileSave('compress-output', 'Salvar PDF comprimido como',
            'Documentos PDF|*.pdf|Todos|*.*', ''));

    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    convertBtn.addEventListener('click', () => {
        const input = inputEl.value.trim();
        if (!input) { setStatus('error', 'Selecione o arquivo PDF de entrada.'); return; }

        statusEl.className = 'status';
        convertBtn.disabled = true;
        setStatus('info', 'Comprimindo…');

        callCSharp('PDF_COMPRESS', {
            inputPath:  input,
            outputPath: outputEl.value.trim() || undefined,
        });
    });

    document.addEventListener('cs-message', e => {
        const msg = e.detail;
        if (msg.action !== 'PDF_COMPRESS') return;

        convertBtn.disabled = false;

        if (msg.type === 'PROGRESS') {
            setStatus('info', msg.message);
        } else if (msg.type === 'SUCCESS') {
            const saved = msg.savedPercent;
            const sign  = saved >= 0 ? '-' : '+';
            setStatus('success',
                `Comprimido com sucesso!\n` +
                `Antes: ${msg.sizeBeforeFmt}  →  Depois: ${msg.sizeAfterFmt}  (${sign}${Math.abs(saved)}%)\n` +
                `Salvo em: ${msg.outputPath}`);
            showToast('success', `PDF comprimido! ${sign}${Math.abs(saved)}% de redução.`, 5000);
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Compressão falhou: ${msg.message}`);
        }
    });
})();
