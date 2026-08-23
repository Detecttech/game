import fs from "node:fs";
import path from "node:path";

function findDistDir(candidates: string[]): string {
  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }
  return candidates[0];
}

export const config = {
  httpPort: Number(process.env.PORT ?? 8080),
  discoveryPort: Number(process.env.DISCOVERY_PORT ?? 7778),
  mode: (process.env.MODE ?? "lan") as "lan" | "wan",
  jwtSecret: process.env.JWT_SECRET ?? "dev-secret-change-me",
  dbPath: process.env.DB_PATH ?? path.join(process.cwd(), "data", "quizbattle.db"),
  serverName: process.env.SERVER_NAME ?? "Classroom QuizBattle",
  webPortalDist: findDistDir([
    path.join(__dirname, "..", "web-portal", "dist"),
    path.join(process.cwd(), "web-portal", "dist"),
    "/app/server/web-portal/dist",
  ]),
  webGLBuildDist: findDistDir([
    path.join(__dirname, "..", "..", "game-client", "webgl-build"),
    path.join(process.cwd(), "..", "game-client", "webgl-build"),
    "/app/game-client/webgl-build",
  ]),
};
