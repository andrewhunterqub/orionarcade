#!/usr/bin/env node
// Minimal local server for testing the arcade and Unity WebGL builds.
// Unity builds can't run from file:// URLs, and compressed builds need Content-Encoding headers,
// which this server sets for .gz and .br files.
//
//   npm run serve            -> http://localhost:8080
//   PORT=3000 npm run serve

import http from "node:http";
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import path from "node:path";
import { SITE } from "./lib.mjs";

const PORT = Number(process.env.PORT) || 8080;

const TYPES = {
  ".html": "text/html; charset=utf-8",
  ".js": "application/javascript",
  ".mjs": "application/javascript",
  ".css": "text/css",
  ".json": "application/json",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".ico": "image/x-icon",
  ".wasm": "application/wasm",
  ".data": "application/octet-stream",
  ".unityweb": "application/octet-stream",
};

function headersFor(file) {
  let name = file;
  let encoding;
  if (name.endsWith(".gz")) [name, encoding] = [name.slice(0, -3), "gzip"];
  else if (name.endsWith(".br")) [name, encoding] = [name.slice(0, -3), "br"];
  const headers = { "Content-Type": TYPES[path.extname(name).toLowerCase()] || "application/octet-stream", "Cache-Control": "no-cache" };
  if (encoding) headers["Content-Encoding"] = encoding;
  return headers;
}

const server = http.createServer(async (req, res) => {
  try {
    let urlPath = decodeURIComponent(new URL(req.url, "http://localhost").pathname);
    if (urlPath.endsWith("/")) urlPath += "index.html";
    const file = path.resolve(SITE, "." + urlPath);
    if (file !== SITE && !file.startsWith(SITE + path.sep)) throw Object.assign(new Error(), { code: "ENOENT" });

    const info = await stat(file);
    if (info.isDirectory()) {
      res.writeHead(301, { Location: urlPath + "/" }).end();
      return;
    }
    res.writeHead(200, { ...headersFor(file), "Content-Length": info.size });
    createReadStream(file).pipe(res);
  } catch (err) {
    res.writeHead(err.code === "ENOENT" ? 404 : 500, { "Content-Type": "text/plain" });
    res.end(err.code === "ENOENT" ? "Not found" : "Server error");
  }
});

server.listen(PORT, () => console.log(`Orion Arcade running at http://localhost:${PORT}`));
