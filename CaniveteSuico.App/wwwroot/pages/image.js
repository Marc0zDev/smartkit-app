// pages/image.js — Image Converter tab logic

(function () {
    const inputEl       = document.getElementById('img-input');
    const formatEl      = document.getElementById('img-format');
    const convertBtn    = document.getElementById('img-convert-btn');
    const browseInBtn   = document.getElementById('img-browse-input');
    const statusEl      = document.getElementById('img-status');

    const IMG_FILTER = 'Imagens|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif|Todos os arquivos|*.*';

    browseInBtn.addEventListener('click', () =>
        browseFile('img-input', 'Selecionar imagem de entrada', IMG_FILTER, ''));

    function setStatus(type, msg) {
        statusEl.className = `status visible ${type}`;
        statusEl.textContent = msg;
    }

    convertBtn.addEventListener('click', () => {
        const inputPath = inputEl.value.trim();
        if (!inputPath) { setStatus('error', 'Por favor, informe o caminho do arquivo de entrada.'); return; }

        statusEl.className = 'status';
        convertBtn.disabled = true;
        setStatus('info', 'Convertendo imagem…');

        callCSharp('IMAGE_CONVERT', {
            inputPath,
            outputFormat: formatEl.value,
        });
    });

    document.addEventListener('cs-message', (e) => {
        const msg = e.detail;
        if (msg.action !== 'IMAGE_CONVERT') return;

        convertBtn.disabled = false;

        if (msg.type === 'SUCCESS') {
            setStatus('success', `Convertido com sucesso! Salvo em: ${msg.outputPath}`);
            showToast('success', `Imagem convertida!\n${msg.outputPath}`, 5000);
        } else if (msg.type === 'ERROR') {
            setStatus('error', `Erro: ${msg.message}`);
            showToast('error', `Conversão falhou: ${msg.message}`);
        }
    });
})();
