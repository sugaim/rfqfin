import { existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const apiProject = resolve(repositoryRoot, "src", "Rfq.Api", "Rfq.Api.csproj");
const webRoot = resolve(repositoryRoot, "src", "Rfq.Web");
const openApiFile = resolve(repositoryRoot, "artifacts", "openapi", "Rfq.Api.json");
const generatedTypeFile = resolve(webRoot, "src", "generated", "api-schema.ts");
const openApiTypescriptCli = resolve(
  webRoot,
  "node_modules",
  "openapi-typescript",
  "bin",
  "cli.js",
);

function run(command, args, workingDirectory = repositoryRoot) {
  const result = spawnSync(command, args, {
    cwd: workingDirectory,
    stdio: "inherit",
    shell: false,
  });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

mkdirSync(dirname(openApiFile), { recursive: true });
run("dotnet", ["build", apiProject, "--nologo"]);

if (!existsSync(openApiFile)) {
  throw new Error(`OpenAPI document was not generated: ${openApiFile}`);
}

if (!existsSync(openApiTypescriptCli)) {
  throw new Error(
    "openapi-typescript is not installed. Run npm install in src/Rfq.Web first.",
  );
}

mkdirSync(dirname(generatedTypeFile), { recursive: true });
run(process.execPath, [openApiTypescriptCli, openApiFile, "--output", generatedTypeFile]);
