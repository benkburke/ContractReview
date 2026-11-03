import {
  HubConnectionBuilder,
  HubConnectionState,
  type HubConnection,
} from "@microsoft/signalr";
import { config } from "@/config";
import { hubMethods, hubRoutes } from "@/constants/realtime";
import type { ClaimProgressEvent } from "@/realtime/contracts";

type Handler = (event: ClaimProgressEvent) => void;

// One module-level connection, ref-counted so a StrictMode double mount
// (mount, unmount, mount) never leaves two connections open.
let connection: HubConnection | null = null;
let refCount = 0;
const handlers = new Set<Handler>();

function dispatch(event: ClaimProgressEvent) {
  handlers.forEach((handler) => handler(event));
}

function open(): HubConnection {
  const hub = new HubConnectionBuilder()
    .withUrl(`${config.apiUrl}${hubRoutes.claimProgress}`, {
      withCredentials: true,
    })
    .withAutomaticReconnect()
    .build();
  hub.on(hubMethods.evaluationProgress, dispatch);
  // Progress is optional: a failed start is swallowed and submits still work.
  hub.start().catch(() => undefined);
  return hub;
}

/** Starts the connection if this is the first user; returns the release function. */
export function acquireClaimProgress(): () => void {
  refCount += 1;
  connection ??= open();
  let released = false;
  return () => {
    if (released) return;
    released = true;
    refCount -= 1;
    if (refCount > 0 || !connection) return;
    const stopping = connection;
    connection = null;
    stopping.off(hubMethods.evaluationProgress, dispatch);
    stopping.stop().catch(() => undefined);
  };
}

export function subscribeClaimProgress(handler: Handler): () => void {
  handlers.add(handler);
  return () => {
    handlers.delete(handler);
  };
}

/** The server-issued connection id, only while the hub is connected. */
export function getClaimProgressConnectionId(): string | null {
  if (connection?.state !== HubConnectionState.Connected) return null;
  return connection.connectionId ?? null;
}
