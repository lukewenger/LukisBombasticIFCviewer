'use strict';
// Patches all native .node addon imports inside @loaders.gl/polyfills/dist/ that
// are not compiled into the npm package. Each is replaced with an equivalent
// Node.js built-in implementation. Node.js 16+ provides everything needed.
const fs   = require('fs');
const path = require('path');

const DIST = '/usr/local/lib/node_modules/@xeokit/xeokit-convert/node_modules/@loaders.gl/polyfills/dist';

// ─── helpers ─────────────────────────────────────────────────────────────────

function patchFile(relPath, patches) {
  const full = path.join(DIST, relPath);
  if (!fs.existsSync(full)) { console.log(`–  ${relPath}: file not found, skipping`); return; }

  let src = fs.readFileSync(full, 'utf8');
  let changed = false;

  for (const { needle, regex, replacement, tag } of patches) {
    if (src.includes(tag || (typeof needle === 'string' ? needle : tag))) {
      console.log(`–  ${relPath}: already patched (${tag})`);
      continue;
    }
    if (regex) {
      // Regex-based replacement: more resilient to minor version differences.
      if (regex.test(src)) {
        src = src.replace(regex, replacement);
        console.log(`✓  ${relPath}: patched via regex /${regex.source.slice(0, 60)}/`);
        changed = true;
      } else {
        console.log(`?  ${relPath}: regex not matched — /${regex.source.slice(0, 60)}/`);
      }
    } else if (src.includes(needle)) {
      src = src.replace(needle, replacement);
      console.log(`✓  ${relPath}: patched "${needle.slice(0, 60)}"`);
      changed = true;
    } else {
      console.log(`?  ${relPath}: needle not found — "${needle.slice(0, 60)}"`);
    }
  }

  if (changed) fs.writeFileSync(full, src);
}

// ─── index.js ────────────────────────────────────────────────────────────────
// 1. btoa / atob  — Node.js 16+ globals
// 2. require-utils — replaced with fs + module built-ins

patchFile('index.js', [
  {
    needle:      "import { atob, btoa } from './buffer/btoa.node';",
    replacement: "const{atob,btoa}=globalThis;/*btoa.node removed: Node.js>=16 native*/",
    tag:         '/*btoa.node removed',
  },
  {
    // Match any "import { ... } from '...require-utils.node'" regardless of
    // which named exports are present — different patch versions of
    // @loaders.gl/polyfills export slightly different symbols.
    regex: /^import \{[^}]+\} from ['"](?:\.\/)?load-library\/require-utils\.node['"];?$/m,
    replacement: [
      "/* require-utils.node removed: replaced with Node.js built-ins */",
      "import { readFileSync as _polyfill_rfs } from 'node:fs';",
      "import { createRequire as _polyfill_cr } from 'node:module';",
      "const _polyfill_require = _polyfill_cr(import.meta.url);",
      "const readFileAsArrayBuffer = (p) => { const b=_polyfill_rfs(p); return b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength); };",
      "const readFileAsText = (p,o) => _polyfill_rfs(p,(o&&o.encoding)||'utf8');",
      "const requireFromFile = (p) => _polyfill_require(p);",
      "const requireFromString = (code,p) => { const m={exports:{}}; new Function('module','exports','require',code)(m,m.exports,_polyfill_require); return m.exports; };",
    ].join('\n'),
    tag: '/* require-utils.node removed',
  },
]);

// ─── file/file-reader.js ─────────────────────────────────────────────────────
// atob — Node.js 16+ global

patchFile('file/file-reader.js', [
  {
    needle:      "import { atob } from '../buffer/btoa.node';",
    replacement: "const{atob}=globalThis;/*btoa.node/atob removed: Node.js>=16 native*/",
    tag:         '/*btoa.node/atob removed',
  },
]);

// ─── images/encode-image-node.js ─────────────────────────────────────────────
// bufferToArrayBuffer — trivial Buffer→ArrayBuffer conversion

patchFile('images/encode-image-node.js', [
  {
    needle:      "import { bufferToArrayBuffer } from '../buffer/to-array-buffer.node';",
    replacement: "/* to-array-buffer.node removed: replaced with inline Buffer→ArrayBuffer */\nconst bufferToArrayBuffer = (buf) => buf.buffer.slice(buf.byteOffset, buf.byteOffset + buf.byteLength);",
    tag:         '/* to-array-buffer.node removed',
  },
]);

// ─── filesystems/fetch-node.js ───────────────────────────────────────────────
// decompressReadStream — pipe through Node.js zlib based on content-encoding header

const DECOMPRESS_IMPL = [
  "/* stream-utils.node removed: replaced with Node.js zlib */",
  "import _zlib from 'node:zlib';",
  "const decompressReadStream = (readStream, headers) => {",
  "  const enc = headers && (typeof headers.get==='function' ? headers.get('content-encoding') : headers['content-encoding']);",
  "  if (!enc) return readStream;",
  "  if (enc==='gzip')    return readStream.pipe(_zlib.createGunzip());",
  "  if (enc==='br')      return readStream.pipe(_zlib.createBrotliDecompress());",
  "  if (enc==='deflate') return readStream.pipe(_zlib.createInflate());",
  "  return readStream;",
  "};",
].join('\n');

patchFile('filesystems/fetch-node.js', [
  {
    needle:      "import { decompressReadStream } from './stream-utils.node';",
    replacement: DECOMPRESS_IMPL,
    tag:         '/* stream-utils.node removed',
  },
]);

// ─── fetch/response-polyfill.js ──────────────────────────────────────────────
// decompressReadStream + concatenateReadStream

const DECOMPRESS_AND_CONCAT_IMPL = [
  "/* stream-utils.node removed: replaced with Node.js zlib + stream */",
  "import _zlib from 'node:zlib';",
  "const decompressReadStream = (readStream, headers) => {",
  "  const enc = headers && (typeof headers.get==='function' ? headers.get('content-encoding') : headers['content-encoding']);",
  "  if (!enc) return readStream;",
  "  if (enc==='gzip')    return readStream.pipe(_zlib.createGunzip());",
  "  if (enc==='br')      return readStream.pipe(_zlib.createBrotliDecompress());",
  "  if (enc==='deflate') return readStream.pipe(_zlib.createInflate());",
  "  return readStream;",
  "};",
  "const concatenateReadStream = (readStream) => new Promise((resolve, reject) => {",
  "  const chunks = [];",
  "  readStream.on('data', c => chunks.push(Buffer.isBuffer(c) ? c : Buffer.from(c)));",
  "  readStream.on('end', () => { const b=Buffer.concat(chunks); resolve(b.buffer.slice(b.byteOffset,b.byteOffset+b.byteLength)); });",
  "  readStream.on('error', reject);",
  "});",
].join('\n');

patchFile('fetch/response-polyfill.js', [
  {
    needle:      "import { decompressReadStream, concatenateReadStream } from '../filesystems/stream-utils.node';",
    replacement: DECOMPRESS_AND_CONCAT_IMPL,
    tag:         '/* stream-utils.node removed',
  },
]);

// ─── Final check: any remaining unpatched .node imports? ─────────────────────
let warnCount = 0;
function scanForRemaining(dir) {
  for (const f of fs.readdirSync(dir)) {
    const full = path.join(dir, f);
    if (fs.statSync(full).isDirectory()) { scanForRemaining(full); continue; }
    if (!f.endsWith('.js')) continue;
    const src = fs.readFileSync(full, 'utf8');
    src.split('\n').forEach((line, i) => {
      if (/^import\b/.test(line) && /\.node['"]/.test(line)) {
        process.stderr.write(`WARNING unpatched .node import: ${full}:${i+1}: ${line.trim()}\n`);
        warnCount++;
      }
    });
  }
}
scanForRemaining(DIST);
if (warnCount === 0) {
  console.log('✓  No remaining unpatched .node imports');
} else {
  process.stderr.write(`\nERROR: ${warnCount} unpatched .node import(s) remain — Docker build must fail.\n`);
  process.exit(1);
}
console.log('Done.');
