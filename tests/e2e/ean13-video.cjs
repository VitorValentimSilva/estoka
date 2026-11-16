// Gera um vídeo .y4m (formato aceito pelo Chrome como "câmera falsa") com um código EAN-13 desenhado.
// Usado pelo E2E do leitor de código de barras. Sem dependências.
const fs = require('node:fs');

const L = ['0001101', '0011001', '0010011', '0111101', '0100011', '0110001', '0101111', '0111011', '0110111', '0001011'];
const G = ['0100111', '0110011', '0011011', '0100001', '0011101', '0111001', '0000101', '0010001', '0001001', '0010111'];
const R = L.map(p => p.split('').map(b => (b === '0' ? '1' : '0')).join(''));
const PARITY = ['LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG', 'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL'];

function ean13Modules(code) {
  const d = code.split('').map(Number);
  let bits = '101';
  for (let i = 1; i <= 6; i++) bits += (PARITY[d[0]][i - 1] === 'L' ? L : G)[d[i]];
  bits += '01010';
  for (let i = 7; i <= 12; i++) bits += R[d[i]];
  return bits + '101';
}

function checkDigit(body) {
  let sum = 0;
  for (let i = 0; i < body.length; i++) sum += Number(body[i]) * ((body.length - i) % 2 === 1 ? 3 : 1);
  return (10 - (sum % 10)) % 10;
}

/** Mesmo algoritmo do seed (DemoDataSeeder.DemoBarcode). */
function demoBarcode(categoryIndex, item) {
  const body = `204512${String(categoryIndex).padStart(2, '0')}${String(item).padStart(4, '0')}`;
  return body + checkDigit(body);
}

function writeY4m(file, code, { width = 640, height = 480, frames = 30 } = {}) {
  const bits = ean13Modules(code);
  const module = 4;
  const barWidth = bits.length * module;
  const x0 = Math.floor((width - barWidth) / 2);
  const y0 = Math.floor(height * 0.3), y1 = Math.floor(height * 0.7);
  const Y = Buffer.alloc(width * height, 235);
  for (let y = y0; y < y1; y++)
    for (let i = 0; i < bits.length; i++)
      if (bits[i] === '1') Y.fill(16, y * width + x0 + i * module, y * width + x0 + (i + 1) * module);
  const UV = Buffer.alloc((width / 2) * (height / 2) * 2, 128);
  const header = Buffer.from(`YUV4MPEG2 W${width} H${height} F10:1 Ip A1:1 C420jpeg\n`);
  const frame = Buffer.concat([Buffer.from('FRAME\n'), Y, UV]);
  fs.writeFileSync(file, Buffer.concat([header, ...Array(frames).fill(frame)]));
}

module.exports = { demoBarcode, writeY4m, checkDigit };
