﻿using RuriLib.Attributes;
using RuriLib.Logging;
using RuriLib.Models.Bots;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Websocket.Client;

namespace RuriLib.Blocks.Requests.WebSocket
{
    [BlockCategory("Web Sockets", "Blocks to send and receive messages through websockets", "#addfad")]
    public static class Methods
    {
        [Block("Connects to a Web Socket", name = "WebSocket Connect",
            extraInfo = "Only works with HTTP proxies or without any proxy")]
        public static async Task WsConnect(BotData data, string url, int keepAliveMilliseconds = 5000)
        {
            data.Logger.LogHeader();

            IWebProxy proxy = null;
            if (data.UseProxy && data.Proxy is not null)
            {
                if (data.Proxy.Type != Models.Proxies.ProxyType.Http)
                {
                    throw new NotSupportedException("Only http proxies are supported");
                }
                else
                {
                    proxy = new WebProxy(data.Proxy.Host, data.Proxy.Port);

                    if (data.Proxy.NeedsAuthentication)
                    {
                        proxy.Credentials = new NetworkCredential(data.Proxy.Username, data.Proxy.Password);
                    }
                }
            }

            var factory = new Func<ClientWebSocket>(() => new ClientWebSocket
            {
                Options =
                {
                    KeepAliveInterval = TimeSpan.FromMilliseconds(keepAliveMilliseconds),
                    Proxy = proxy
                }
            });

            var wsMessages = new List<string>();
            data.SetObject("wsMessages", wsMessages);

            var ws = new WebsocketClient(new Uri(url), factory)
            {
                IsReconnectionEnabled = false,
                ErrorReconnectTimeout = null
            };

            ws.MessageReceived.Subscribe(msg => 
            {
                lock (wsMessages)
                {
                    wsMessages.Add(msg.Text);
                }
            });
            
            ws.DisconnectionHappened.Subscribe(msg =>
            {   
                if (msg.Exception != null)
                {
                    throw msg.Exception;
                }
            });

            // Connect
            await ws.Start().ConfigureAwait(false);

            if (!ws.IsRunning)
            {
                throw new Exception("Failed to connect to the websocket");
            }

            data.SetObject("webSocket", ws);

            data.Logger.Log($"The Web Socket client connected to {url}", LogColors.MossGreen);
        }

        [Block("Sends a message on the Web Socket", name = "WebSocket Send")]
        public static void WsSend(BotData data, string message)
        {
            data.Logger.LogHeader();

            var ws = GetSocket(data);
            ws.Send(message);

            data.Logger.Log($"Sent {message} to the server", LogColors.MossGreen);
        }

        [Block("Gets unread messages that the server sent since the last read", name = "WebSocket Read")]
        public static List<string> WsRead(BotData data)
        {
            data.Logger.LogHeader();

            var messages = GetMessages(data);
            var cloned = messages.Select(m => m).ToList();

            lock (messages)
                messages.Clear();

            data.Logger.Log($"Unread messages from server", LogColors.MossGreen);
            data.Logger.Log(cloned, LogColors.MossGreen);

            return cloned;
        }

        [Block("Disconnects the existing Web Socket", name = "WebSocket Disconnect")]
        public static void WsDisconnect(BotData data)
        {
            data.Logger.LogHeader();

            var ws = GetSocket(data);
            ws.Stop(WebSocketCloseStatus.NormalClosure, null);

            data.Logger.Log("Closed the WebSocket", LogColors.MossGreen);
        }

        private static WebsocketClient GetSocket(BotData data)
            => data.TryGetObject<WebsocketClient>("webSocket") ?? throw new NullReferenceException("You must open a websocket connection first");

        private static List<string> GetMessages(BotData data)
            => data.TryGetObject<List<string>>("wsMessages") ?? throw new NullReferenceException("You must open a websocket connection first");
    }
}
