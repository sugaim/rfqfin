import { existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { spawnSync } from "node:child_process";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const apiProject = resolve(repositoryRoot, "src", "Rfq.Api", "Rfq.Api.csproj");
const webRoot = resolve(repositoryRoot, "src", "Rfq.Web");
const openApiFile = resolve(
  repositoryRoot,
  "artifacts",
  "openapi",
  "Rfq.Api.json",
);
const generatedClientFile = resolve(webRoot, "src", "generated", "rfqApi.ts");
const codegenConfig = resolve(webRoot, "openapi-config.mjs");
const codegenCli = resolve(
  webRoot,
  "node_modules",
  "@rtk-query",
  "codegen-openapi",
  "lib",
  "bin",
  "cli.mjs",
);
const prettierCli = resolve(
  webRoot,
  "node_modules",
  "prettier",
  "bin",
  "prettier.cjs",
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

if (!existsSync(codegenCli)) {
  throw new Error(
    "@rtk-query/codegen-openapi is not installed. Run npm install in src/Rfq.Web first.",
  );
}

if (!existsSync(prettierCli)) {
  throw new Error(
    "Prettier is not installed. Run npm install in src/Rfq.Web first.",
  );
}

mkdirSync(dirname(generatedClientFile), { recursive: true });
run(process.execPath, [codegenCli, codegenConfig], webRoot);
run(
  process.execPath,
  [prettierCli, "--write", generatedClientFile, codegenConfig],
  webRoot,
);
