using System;

/**
 * Group 7
 * Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 * Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)
**/

namespace ChatServerTemplate
{
	delegate void WebSocketClientEventHandler(object source, WebSocketClientEventArgs e);
	delegate void TextMessageEventHandler(object source, TextMessageEventArgs e);

	class WebSocketClientEventArgs: EventArgs
	{
		private readonly WebSocketClient client;

		public WebSocketClientEventArgs(WebSocketClient client)
		{
			if(client == null) throw new ArgumentNullException("client");
			this.client = client;
		}

		public WebSocketClient Client
		{
			get { return client; }
		}
	}

	sealed class TextMessageEventArgs: WebSocketClientEventArgs
	{
		private readonly string message;

		public TextMessageEventArgs(WebSocketClient client, string message)
			: base(client)
		{
			if(message == null) throw new ArgumentNullException("message");
			this.message = message;
		}

		public string Message
		{
			get { return message; }
		}
	}
}
