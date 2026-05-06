// bridge.js — communication layer between Vanilla JS and C# via WebView2

/**
 * Send a command to the C# backend.
 * @param {string} action  e.g. "YOUTUBE_DOWNLOAD"
 * @param {object} data    payload object
 */
function callCSharp(action, data) {
    const message = JSON.stringify({ action, data });
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(message);
    } else {
        console.warn('[Bridge] Not running inside WebView2. Message not sent:', message);
    }
}

/**
 * Open a native file-open dialog and populate an input element with the result.
 * @param {string} inputId     id of the <input> to populate
 * @param {string} title       dialog title
 * @param {string} [filter]    e.g. "Imagens|*.jpg;*.png"
 * @param {string} [initialDir]
 */
function browseFile(inputId, title, filter, initialDir) {
    callCSharp('OPEN_DIALOG', {
        dialogType: 'file',
        requestId: inputId,
        title,
        filter: filter || 'Todos os arquivos|*.*',
        initialDir: initialDir || '',
    });
}

/**
 * Open a native save-file dialog and populate an input element with the result.
 */
function browseFileSave(inputId, title, filter, initialDir) {
    callCSharp('OPEN_DIALOG', {
        dialogType: 'save',
        requestId: inputId,
        title,
        filter: filter || 'Todos os arquivos|*.*',
        initialDir: initialDir || '',
    });
}

/**
 * Open a native folder-browser dialog and populate an input element with the result.
 */
function browseFolder(inputId, title, initialDir) {
    callCSharp('OPEN_DIALOG', {
        dialogType: 'folder',
        requestId: inputId,
        title,
        initialDir: initialDir || '',
    });
}

// ── Incoming message router ────────────────────────────────────────────────

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (e) => {
        let msg;
        try {
            msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
        } catch {
            console.error('[Bridge] Failed to parse C# message:', e.data);
            return;
        }

        // Auto-populate input fields from dialog results, then still broadcast
        // so pages that added files programmatically (e.g. merge PDF) can react.
        if (msg.type === 'DIALOG_RESULT' && msg.requestId) {
            const input = document.getElementById(msg.requestId);
            if (input && msg.path) input.value = msg.path;
            // fall through — broadcast below
        }

        // Handle update notifications inline — no page module needed
        if (msg.type === 'UPDATE_AVAILABLE') {
            showUpdateBanner(msg.version);
            return;
        }
        if (msg.type === 'UPDATE_PROGRESS') {
            showUpdateProgress(msg.percent, msg.message);
            return;
        }

        // Broadcast everything else as a DOM event for page modules
        document.dispatchEvent(new CustomEvent('cs-message', { detail: msg }));
    });
}

// ── Update banner ──────────────────────────────────────────────────────────

function showUpdateBanner(version) {
    const banner      = document.getElementById('update-banner');
    const bannerText  = document.getElementById('update-banner-text');
    const installBtn  = document.getElementById('update-install-btn');
    const dismissBtn  = document.getElementById('update-dismiss-btn');
    const progressWrap = document.getElementById('update-progress-wrap');

    if (!banner) return;
    bannerText.textContent = `Versão ${version} disponível`;
    banner.style.display = 'flex';

    installBtn.addEventListener('click', () => {
        installBtn.disabled  = true;
        dismissBtn.disabled  = true;
        installBtn.textContent = 'Baixando…';
        progressWrap.style.display = 'flex';
        callCSharp('INSTALL_UPDATE', {});
    }, { once: true });

    dismissBtn.addEventListener('click', () => {
        banner.style.display = 'none';
    }, { once: true });
}

function showUpdateProgress(percent, message) {
    const fill = document.getElementById('update-progress-fill');
    const msg  = document.getElementById('update-progress-msg');
    if (fill) fill.style.width = percent + '%';
    if (msg && message) msg.textContent = message;
}

// ── Toast notification system ──────────────────────────────────────────────

(function () {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.style.cssText =
        'position:fixed;bottom:24px;right:24px;display:flex;flex-direction:column;' +
        'gap:10px;z-index:9999;pointer-events:none;';
    document.addEventListener('DOMContentLoaded', () => document.body.appendChild(container));

    /**
     * Show a toast notification.
     * @param {'success'|'error'|'info'} type
     * @param {string} message
     * @param {number} [duration=4000]
     */
    window.showToast = function (type, message, duration = 4000) {
        const toast = document.createElement('div');
        const colors = {
            success: { bg: '#1b3128', border: '#2d5240', text: '#4caf7d', icon: '✓' },
            error:   { bg: '#2c1b1b', border: '#5c2e2e', text: '#ff5c5c', icon: '✕' },
            info:    { bg: '#1b1f34', border: '#2e3347', text: '#7b82a6', icon: 'ℹ' },
        };
        const c = colors[type] || colors.info;

        toast.style.cssText =
            `background:${c.bg};border:1px solid ${c.border};color:${c.text};` +
            'padding:12px 18px;border-radius:8px;font-size:0.85rem;max-width:340px;' +
            'pointer-events:auto;box-shadow:0 4px 16px rgba(0,0,0,0.5);' +
            'display:flex;align-items:flex-start;gap:10px;' +
            'animation:toast-in 0.2s ease;';
        toast.innerHTML =
            `<span style="font-weight:700;font-size:1rem;flex-shrink:0">${c.icon}</span>` +
            `<span>${message}</span>`;

        container.appendChild(toast);

        const remove = () => {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.3s';
            setTimeout(() => toast.remove(), 300);
        };
        setTimeout(remove, duration);
        toast.addEventListener('click', remove);
    };

    // Inject toast keyframe animation once
    const style = document.createElement('style');
    style.textContent = '@keyframes toast-in{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:none}}';
    document.head.appendChild(style);
})();
