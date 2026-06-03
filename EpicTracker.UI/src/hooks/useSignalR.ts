import { useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';

type Handlers = Record<string, (...args: unknown[]) => void>;

export function useSignalR(url: string, handlers: Handlers): boolean {
  const [connected, setConnected] = useState(false);
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect()
      .build();

    for (const event of Object.keys(handlersRef.current)) {
      connection.on(event, (...args) => handlersRef.current[event]?.(...args));
    }

    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => { connection.stop(); };
  }, [url]);

  return connected;
}
