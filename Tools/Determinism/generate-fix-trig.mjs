// 生成 AFPS.Math 的 4096 槽常量表：node Tools/Determinism/generate-fix-trig.mjs
// 全程 BigInt、30 位十进制精度、20 项 Taylor；不使用平台 Sin/Cos。
import { writeFile } from 'node:fs/promises';
const scale = 10n ** 30n;
const pi = 3141592653589793238462643383279n;
const quarter = [];
for (let i = 0; i <= 1024; i++) {
    const x = pi * BigInt(i) / 2048n;
    const squared = x * x / scale;
    let term = x, sum = x;
    for (let k = 1n; k < 20n; k++) {
        term = -term * squared / (scale * (2n*k) * (2n*k+1n));
        sum += term;
    }
    quarter.push(Number((sum * 65536n + scale/2n) / scale));
}
const table = [];
for (let i = 0; i < 4096; i++) {
    const quadrant = i >> 10, offset = i & 1023;
    const value = quarter[(quadrant & 1) === 0 ? offset : 1024-offset];
    table.push(quadrant < 2 ? value : -value);
}
const rows = [];
for (let i = 0; i < table.length; i += 16) rows.push('            '+table.slice(i,i+16).join(', ')+',');
const output = '// 自动生成：Tools/Determinism/generate-fix-trig.mjs；请勿手改。\nnamespace AFPS.Math\n{\n    public static partial class FixTrig\n    {\n        private static readonly int[] SinTableRaw =\n        {\n'+rows.join('\n')+'\n        };\n    }\n}\n';
await writeFile(new URL('../../Assets/_Game/Scripts/Math/FixTrig.Table.cs', import.meta.url),output,'utf8');
