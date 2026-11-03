// 自有合成起跳音：44.1kHz单声道PCM16，0.18秒；固定种子仅用于声音纹理。
const fs = require('fs');
const path = require('path');
const output = path.resolve(__dirname, '../../Assets/_Game/Art/Resources/Combat/Jump.wav');
if (fs.existsSync(output)) {
    throw new Error('Refusing to overwrite existing jump audio.');
}
const rate = 44100;
const count = Math.round(rate * 0.18);
const wav = Buffer.alloc(44 + count * 2);
wav.write('RIFF', 0);
wav.writeUInt32LE(36 + count * 2, 4);
wav.write('WAVEfmt ', 8);
wav.writeUInt32LE(16, 16);
wav.writeUInt16LE(1, 20);
wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(rate, 24);
wav.writeUInt32LE(rate * 2, 28);
wav.writeUInt16LE(2, 32);
wav.writeUInt16LE(16, 34);
wav.write('data', 36);
wav.writeUInt32LE(count * 2, 40);
let seed = 23719;
let filtered = 0;
let phase = 0;
for (let i = 0; i < count; i++) {
    const t = i / rate;
    seed = (Math.imul(seed, 1664525) + 1013904223) >>> 0;
    filtered += (((seed / 4294967296) * 2 - 1) - filtered) * 0.28;
    phase += 2 * Math.PI * (170 * Math.exp(-t * 16) + 45) / rate;
    const envelope = Math.min(1, t / 0.006) * Math.exp(-t * 25) * Math.min(1, (0.18 - t) / 0.02);
    const value = (Math.sin(phase) * 0.45 + filtered * 0.55) * envelope * 0.65;
    wav.writeInt16LE(Math.round(Math.max(-1, Math.min(1, value)) * 32767), 44 + i * 2);
}
fs.writeFileSync(output, wav);
console.log(output);
