// 空 catch 治理的 lint 门禁。
// Scans .cs files changed vs HEAD (or files passed as args) for empty catch
// blocks (regex: catch\s*(\([^)]*\))?\s*\{\s*\}, DOTALL). Exit 1 if any found.
import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';

const args = process.argv.slice(2);
let files = args;
if (files.length === 0) {
  const out = execSync('git diff --name-only HEAD', { encoding: 'utf8' }).trim();
  files = out ? out.split('\n') : [];
}
files = files.filter(f => f.endsWith('.cs'));

const pattern = /catch\s*(\([^)]*\))?\s*\{\s*\}/gs;
let bad = 0;
for (const f of files) {
  let src;
  try {
    src = readFileSync(f, 'utf8');
  } catch {
    continue; // deleted or unreadable file: nothing to lint
  }
  const matches = [...src.matchAll(pattern)];
  for (const m of matches) {
    const line = src.slice(0, m.index).split('\n').length;
    console.error(`${f}:${line}: empty catch block`);
    bad++;
  }
}
if (bad > 0) {
  console.error(`lint: ${bad} empty catch block(s) in changed files`);
  process.exit(1);
}
console.log(`lint: 0 empty catch blocks in ${files.length} changed .cs file(s)`);
