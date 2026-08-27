import express from "express";
import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";
import { config } from "../config";
import { serverInfoRoutes } from "./routes/serverInfoRoutes";
import { authRoutes } from "./routes/authRoutes";
import { classRoutes } from "./routes/classRoutes";
import { rosterRoutes } from "./routes/rosterRoutes";
import { questionBankRoutes, questionRoutes } from "./routes/questionBankRoutes";
import { matchRoutes } from "./routes/matchRoutes";
import { leaderboardRoutes } from "./routes/leaderboardRoutes";
import { studentRoutes } from "./routes/studentRoutes";
import { backupRoutes } from "./routes/backupRoutes";

export function createHttpApp() {
  const app = express();
  app.use(express.json());

  app.use(
    "/api",
    serverInfoRoutes,
    authRoutes,
    classRoutes,
    rosterRoutes,
    questionBankRoutes,
    questionRoutes,
    matchRoutes,
    leaderboardRoutes,
    studentRoutes,
    backupRoutes
  );

  // Mounted before the web-portal's catch-all below, at its own /play prefix so the
  // two static builds (teacher dashboard at /, game client at /play) don't collide on
  // the same origin — same-origin means the WebGL build's fetch/WebSocket calls back to
  // this server need no CORS configuration at all.
  if (fs.existsSync(config.webGLBuildDist)) {
    // High-performance streaming for large WebAssembly file (51MB -> 14MB)
    app.get("/play/Build/webgl-build.wasm", (req, res) => {
      const gzPath = path.join(config.webGLBuildDist, "Build", "webgl-build.wasm.gz");
      const wasmPath = path.join(config.webGLBuildDist, "Build", "webgl-build.wasm");

      const acceptGzip = req.headers["accept-encoding"]?.includes("gzip");

      if (acceptGzip && fs.existsSync(gzPath)) {
        const stat = fs.statSync(gzPath);
        res.writeHead(200, {
          "Content-Type": "application/wasm",
          "Content-Encoding": "gzip",
          "Content-Length": stat.size,
          "Cache-Control": "no-cache, no-store, must-revalidate",
        });
        return fs.createReadStream(gzPath).pipe(res);
      } else if (acceptGzip && fs.existsSync(wasmPath)) {
        res.writeHead(200, {
          "Content-Type": "application/wasm",
          "Content-Encoding": "gzip",
          "Cache-Control": "no-cache, no-store, must-revalidate",
        });
        const gzip = zlib.createGzip({ level: 6 });
        return fs.createReadStream(wasmPath).pipe(gzip).pipe(res);
      } else if (fs.existsSync(wasmPath)) {
        const stat = fs.statSync(wasmPath);
        res.writeHead(200, {
          "Content-Type": "application/wasm",
          "Content-Length": stat.size,
          "Cache-Control": "no-cache, no-store, must-revalidate",
        });
        return fs.createReadStream(wasmPath).pipe(res);
      } else {
        return res.status(404).send("WASM file not found");
      }
    });

    // High-performance streaming for WebGL Data file (14MB)
    app.get("/play/Build/webgl-build.data", (req, res) => {
      const dataPath = path.join(config.webGLBuildDist, "Build", "webgl-build.data");
      if (!fs.existsSync(dataPath)) {
        return res.status(404).send("Data file not found");
      }

      const acceptGzip = req.headers["accept-encoding"]?.includes("gzip");
      if (acceptGzip) {
        res.writeHead(200, {
          "Content-Type": "application/octet-stream",
          "Content-Encoding": "gzip",
          "Cache-Control": "no-cache, no-store, must-revalidate",
        });
        const gzip = zlib.createGzip({ level: 6 });
        return fs.createReadStream(dataPath).pipe(gzip).pipe(res);
      } else {
        const stat = fs.statSync(dataPath);
        res.writeHead(200, {
          "Content-Type": "application/octet-stream",
          "Content-Length": stat.size,
          "Cache-Control": "no-cache, no-store, must-revalidate",
        });
        return fs.createReadStream(dataPath).pipe(res);
      }
    });

    app.use(
      "/play",
      express.static(config.webGLBuildDist, {
        index: "index.html",
        setHeaders: (res, filePath) => {
          res.setHeader("Cache-Control", "no-cache, no-store, must-revalidate");
          if (filePath.endsWith(".wasm")) {
            res.setHeader("Content-Type", "application/wasm");
          } else if (filePath.endsWith(".data")) {
            res.setHeader("Content-Type", "application/octet-stream");
          } else if (filePath.endsWith(".js") || filePath.endsWith(".framework.js")) {
            res.setHeader("Content-Type", "application/javascript");
          }
        },
      })
    );
  }

  if (fs.existsSync(config.webPortalDist)) {
    app.use(express.static(config.webPortalDist, {
      setHeaders: (res, filePath) => {
        if (filePath.endsWith("index.html")) {
          res.setHeader("Cache-Control", "no-cache, no-store, must-revalidate");
        }
      },
    }));
    app.get("*", (req, res) => {
      if (req.path.startsWith("/play") || req.path.startsWith("/api") || req.path.startsWith("/ws")) {
        return res.status(404).send("Not Found");
      }
      res.setHeader("Cache-Control", "no-cache, no-store, must-revalidate");
      res.sendFile(path.join(config.webPortalDist, "index.html"));
    });
  }

  // Error logging middleware
  app.use((err: any, _req: express.Request, res: express.Response, _next: express.NextFunction) => {
    console.error("[server] HTTP error:", err);
    if (!res.headersSent) {
      res.status(500).send(err?.message ?? "Internal Server Error");
    }
  });

  return app;
}
