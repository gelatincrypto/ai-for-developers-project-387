import { readdir, readFile, writeFile } from 'node:fs/promises'

const root = new URL('../../src/Calendar.Api/Generated/OpenApi/src/Calendar.Api.Generated/', import.meta.url)

async function enableNullable(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const file = new URL(entry.name, directory)
    if (entry.isDirectory()) {
      await enableNullable(new URL(`${entry.name}/`, directory))
    } else if (entry.name.endsWith('.cs')) {
      const source = await readFile(file, 'utf8')
      const normalized = source.replace(/[ \t]+(?=\r?\n|$)/g, '')
      const header = directory.pathname.endsWith('/Models/')
        ? '#nullable enable\n#pragma warning disable CS8618\n'
        : '#nullable enable\n'
      await writeFile(file, normalized.replace(/^#nullable enable\n(?:#pragma warning disable CS8618\n)?/, '').replace(/^/, header))
    }
  }
}

await enableNullable(root)
