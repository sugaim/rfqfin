import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn, spawnSync } from 'node:child_process'

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const apiProject = resolve(repositoryRoot, 'src', 'Rfq.Api', 'Rfq.Api.csproj')

const docker = spawnSync('docker', ['compose', 'up', '-d'], {
  cwd: repositoryRoot,
  stdio: 'inherit',
  shell: false,
})

if (docker.error) throw docker.error
if (docker.status !== 0) process.exit(docker.status ?? 1)

const api = spawn('dotnet', ['run', '--project', apiProject], {
  cwd: repositoryRoot,
  stdio: 'inherit',
  shell: false,
})

api.on('error', (error) => {
  console.error(error)
  process.exitCode = 1
})

api.on('exit', (code, signal) => {
  process.exitCode = code ?? (signal === 'SIGINT' ? 130 : 1)
})

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => {
    if (!api.killed) api.kill(signal)
  })
}
