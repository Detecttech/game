import { createServer } from "node:http";
import { config } from "./config";
import { createHttpApp } from "./http/app";
import { createWsServer } from "./ws/wsServer";
import { startDiscoveryResponder } from "./discovery/udpResponder";
import "./db/client";

// Last line of defense: a teacher's classroom session should never go down mid-lesson
// because of one bug in one match. The WS dispatcher and round-timer callback already
// catch the failure modes we know about (see ws/dispatcher.ts, matchEngine/LiveMatchRegistry.ts);
// this just stops anything unforeseen from taking the whole process down with it.
process.on("uncaughtException", (err) => {
  console.error("[server] uncaught exception (server staying up):", err);
});
process.on("unhandledRejection", (err) => {
  console.error("[server] unhandled rejection (server staying up):", err);
});

const app = createHttpApp();
const httpServer = createServer(app);
createWsServer(httpServer);

if (config.mode === "lan") {
  try {
    startDiscoveryResponder();
  } catch (err) {
    console.warn("[server] UDP discovery responder skipped:", err);
  }
}

httpServer.listen(config.httpPort, "0.0.0.0", () => {
  console.log(`[server] HTTP+WS listening on 0.0.0.0:${config.httpPort} (mode=${config.mode})`);
});
