import { cpSync, mkdirSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import * as esbuild from "esbuild";

const root = dirname(fileURLToPath(import.meta.url));
const dist = resolve(root, "dist");
const shared = resolve(root, "..", "shared");

rmSync(dist, { recursive: true, force: true });

const common = {
  bundle: true,
  minify: false,
  sourcemap: true,
  target: ["chrome110"],
  logLevel: "info",
  absWorkingDir: root,
  outdir: "dist",
  loader: { ".css": "text" },
  alias: { "@shared": shared }
};

await esbuild.build({
  ...common,
  format: "esm",
  entryPoints: { "sw/service-worker": "src/sw/service-worker.ts" }
});

await esbuild.build({
  ...common,
  format: "iife",
  entryPoints: {
    "content/youtube": "src/content/youtube.ts",
    "popup/popup": "src/popup/popup.ts",
    "options/options": "src/options/options.ts"
  }
});

cpSync(resolve(root, "static"), dist, { recursive: true });
mkdirSync(resolve(dist, "styles"), { recursive: true });
cpSync(resolve(shared, "styles", "tokens.css"), resolve(dist, "styles", "tokens.css"));
cpSync(resolve(shared, "styles", "components.css"), resolve(dist, "styles", "components.css"));
