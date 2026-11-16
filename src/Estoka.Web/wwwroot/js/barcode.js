// Leitura de código de barras pela câmera.
// Usa a BarcodeDetector API nativa quando existe (Chrome/Edge no Android, macOS, ChromeOS);
// senão carrega a ZXing (servida localmente em lib/zxing) — funciona também no iPhone/Safari e no Firefox.
const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128'];
let stream = null;
let reader = null;
let stopped = true;

function loadZxing() {
  if (window.ZXing) return Promise.resolve();
  return new Promise((resolve, reject) => {
    const s = document.createElement('script');
    s.src = 'lib/zxing/zxing.min.js';
    s.onload = resolve;
    s.onerror = () => reject(new Error('Não foi possível carregar o leitor.'));
    document.head.appendChild(s);
  });
}

function friendly(err) {
  switch (err && err.name) {
    case 'NotAllowedError': return 'Permissão da câmera negada. Libere o acesso nas configurações do navegador.';
    case 'NotFoundError': case 'OverconstrainedError': return 'Nenhuma câmera encontrada neste dispositivo.';
    case 'NotReadableError': return 'A câmera está em uso por outro aplicativo.';
    case 'SecurityError': return 'A câmera só funciona em conexão segura (HTTPS).';
    default: return (err && err.message) || 'Não foi possível acessar a câmera.';
  }
}

export async function start(video, dotnet) {
  stop();
  stopped = false;
  if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
    return { ok: false, engine: null, error: 'Este navegador não permite acesso à câmera (use HTTPS).' };
  try {
    if ('BarcodeDetector' in window) {
      const supported = await window.BarcodeDetector.getSupportedFormats();
      const formats = FORMATS.filter(f => supported.includes(f));
      if (formats.length > 0) {
        const detector = new window.BarcodeDetector({ formats });
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' }, audio: false });
        video.srcObject = stream;
        await video.play();
        const loop = async () => {
          if (stopped) return;
          try {
            const codes = await detector.detect(video);
            if (codes.length > 0) {
              stopped = true;
              await dotnet.invokeMethodAsync('OnDetected', codes[0].rawValue);
              return;
            }
          } catch { /* quadro ainda não pronto */ }
          requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
        return { ok: true, engine: 'nativo', error: null };
      }
    }
    await loadZxing();
    const hints = new Map();
    hints.set(window.ZXing.DecodeHintType.POSSIBLE_FORMATS, [
      window.ZXing.BarcodeFormat.EAN_13, window.ZXing.BarcodeFormat.EAN_8,
      window.ZXing.BarcodeFormat.UPC_A, window.ZXing.BarcodeFormat.UPC_E, window.ZXing.BarcodeFormat.CODE_128,
    ]);
    reader = new window.ZXing.BrowserMultiFormatReader(hints, 250);
    await reader.decodeFromConstraints({ video: { facingMode: 'environment' }, audio: false }, video, (result) => {
      if (result && !stopped) {
        stopped = true;
        dotnet.invokeMethodAsync('OnDetected', result.getText());
      }
    });
    return { ok: true, engine: 'zxing', error: null };
  } catch (err) {
    stop();
    return { ok: false, engine: null, error: friendly(err) };
  }
}

export function stop() {
  stopped = true;
  if (stream) { stream.getTracks().forEach(t => t.stop()); stream = null; }
  if (reader) { try { reader.reset(); } catch { } reader = null; }
}
