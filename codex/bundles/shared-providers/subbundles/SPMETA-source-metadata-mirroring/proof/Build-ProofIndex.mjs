import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const bundle = 'codex/bundles/shared-providers/';
const unit = bundle + 'subbundles/SPMETA-source-metadata-mirroring/';
const excluded = [
    unit + 'proof/changed-files.json',
    unit + 'proof/transcripts/closure-validation.txt'
];
const git = (...args) => execFileSync('git', args, {
    cwd: root, maxBuffer: 16 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe']
});
const baseline = git('rev-parse', 'HEAD').toString().trim();
const baselinePaths = new Set(git('ls-tree', '-r', '--name-only', '-z', baseline).toString().split('\0'));
const paths = new Set([
    ...git('diff', '--name-only', '-z', 'HEAD').toString().split('\0'),
    ...git('ls-files', '--others', '--exclude-standard', '-z').toString().split('\0')
].filter(value => value && !excluded.includes(value)));
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const files = [...paths].sort().map(file => {
    const before = baselinePaths.has(file) ? hash(git('show', baseline + ':' + file)) : null;
    return {
        path: file.startsWith(bundle) ? 'bundle://' + file.slice(bundle.length) : 'repo://' + file,
        before,
        after: hash(readFileSync(path.resolve(root, file)))
    };
});
process.stdout.write(JSON.stringify({
    baseline,
    beforeMeaning: 'Raw Git blob bytes at baseline, not checkout CRLF; null means newly added.',
    afterMeaning: 'SHA-256 of actual worktree bytes.',
    selfHashExceptions: excluded.map(file => 'bundle://' + file.slice(bundle.length)),
    skillFilesChanged: false,
    files
}, null, 2) + '\n');
