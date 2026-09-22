import { spawnSync } from 'node:child_process'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const webRoot = resolve(repositoryRoot, 'src', 'Rfq.Web')
const prettier = resolve(webRoot, 'node_modules', 'prettier', 'bin', 'prettier.cjs')
const eslint = resolve(webRoot, 'node_modules', 'eslint', 'bin', 'eslint.js')

const projects = {
  cs: {
    root: repositoryRoot,
    check: [
      ['dotnet', ['format', 'Rfq.sln', '--verify-no-changes', '--no-restore', '--severity', 'info']],
    ],
    fix: [['dotnet', ['format', 'Rfq.sln', '--no-restore', '--severity', 'info']]],
  },
  ts: {
    root: webRoot,
    check: [
      [process.execPath, [prettier, '--check', '.']],
      [process.execPath, [eslint, '.', '--max-warnings', '0']],
    ],
    fix: [
      [process.execPath, [eslint, '.', '--fix', '--max-warnings', '0']],
      [process.execPath, [prettier, '--write', '.']],
    ],
  },
}

function usage() {
  console.log(`Usage: node scripts/quality.js [--target cs|ts]... [--fix]

Targets may be repeated or comma-separated. With no --target, both projects run.

Examples:
  node scripts/quality.js
  node scripts/quality.js --target ts --fix
  node scripts/quality.js --target cs --target ts
  node scripts/quality.js --target cs,ts --fix`)
}

function parseArguments(arguments_) {
  const targets = []
  let fix = false

  for (let index = 0; index < arguments_.length; index += 1) {
    const argument = arguments_[index]
    if (argument === '--fix') {
      fix = true
    } else if (argument === '--help' || argument === '-h') {
      usage()
      process.exit(0)
    } else if (argument === '--target') {
      const value = arguments_[index + 1]
      if (!value) throw new Error('--target requires a value.')
      targets.push(...value.split(',').filter(Boolean))
      index += 1
    } else {
      throw new Error(`Unknown argument: ${argument}`)
    }
  }

  return { targets: [...new Set(targets.length ? targets : Object.keys(projects))], fix }
}

function run(command, arguments_, workingDirectory) {
  const result = spawnSync(command, arguments_, {
    cwd: workingDirectory,
    stdio: 'inherit',
    shell: false,
  })
  if (result.error) throw result.error
  if (result.status !== 0) process.exit(result.status ?? 1)
}

try {
  const options = parseArguments(process.argv.slice(2))
  for (const target of options.targets) {
    const project = projects[target]
    if (!project) throw new Error(`Unknown target '${target}'. Expected cs or ts.`)
    console.log(`\n[quality] ${target} (${options.fix ? 'fix' : 'check'})`)
    const steps = options.fix ? project.fix : project.check
    for (const [command, arguments_] of steps) run(command, arguments_, project.root)
  }
} catch (error) {
  console.error(error instanceof Error ? error.message : error)
  usage()
  process.exit(1)
}
