#!/usr/bin/env node
// Install dsh-coord git hooks into a repository.
//   node install.mjs [--root DIR] [--uninstall] [--force]
//
// Hooks installed:
//   pre-push    refuses a push while another session holds the target ref
//   pre-commit  warns when other sessions are active, blocks on a held commit/push lease

import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const coord = path.join(here, 'coord.mjs');

function parse(argv) {
  const flags = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const k = a.slice(2);
      const n = argv[i + 1];
      if (n !== undefined && !n.startsWith('--')) {
        flags[k] = n;
        i++;
      } else flags[k] = true;
    }
  }
  return flags;
}

function git(args, cwd) {
  return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
}

const flags = parse(process.argv.slice(2));
const root = flags.root
  ? path.resolve(String(flags.root))
  : (() => {
      try {
        return git(['rev-parse', '--show-toplevel'], process.cwd());
      } catch {
        return process.cwd();
      }
    })();

let hooksDir;
try {
  hooksDir = path.resolve(root, git(['rev-parse', '--git-path', 'hooks'], root));
} catch {
  hooksDir = path.join(root, '.git', 'hooks');
}
if (!fs.existsSync(hooksDir)) {
  console.error(`not a git repository (no hooks dir at ${hooksDir})`);
  process.exit(1);
}

// git bash treats backslashes as escapes; use forward slashes in the shebang line
const coordForShell = coord.replace(/\\/g, '/');

const hooks = {
  'pre-push': `#!/bin/sh\n# dsh-coord: block pushes that race another session\nexec node "${coordForShell}" guard push "$1" "$2"\n`,
  'pre-commit': `#!/bin/sh\n# dsh-coord: serialize commits across concurrent sessions\nexec node "${coordForShell}" guard commit\n`,
};

if (flags.uninstall) {
  for (const name of Object.keys(hooks)) {
    const p = path.join(hooksDir, name);
    if (!fs.existsSync(p)) continue;
    const body = fs.readFileSync(p, 'utf8');
    if (!body.includes('dsh-coord')) {
      console.log(`skip ${name}: not installed by dsh-coord`);
      continue;
    }
    fs.unlinkSync(p);
    console.log(`removed ${name}`);
  }
  process.exit(0);
}

for (const [name, body] of Object.entries(hooks)) {
  const dest = path.join(hooksDir, name);
  if (fs.existsSync(dest) && !flags.force) {
    const existing = fs.readFileSync(dest, 'utf8');
    console.log(`skip ${name}: already exists (${existing.includes('dsh-coord') ? 'dsh-coord' : 'foreign'}). Use --force to replace.`);
    continue;
  }
  fs.writeFileSync(dest, body, { mode: 0o755 });
  fs.chmodSync(dest, 0o755);
  console.log(`installed ${name}`);
}

console.log(`hooks dir: ${hooksDir}`);
console.log(`cli:       ${coord}`);
console.log(`next:      node "${coord}" init --as <session-name>`);
