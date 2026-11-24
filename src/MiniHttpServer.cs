using System.Net;
using System.Net.Sockets;
using System.Text;

namespace StlTechRelGen;

/// <summary>
/// 极简 HTTP/1.1 服务器，只服务本工具自带的 Web 界面。
///
/// 为什么不用 <see cref="HttpListener"/>：
/// 1. 它在 Windows 上依赖 HTTP.sys，绑定 URL 需要管理员权限或事先注册 URL ACL，
///    对「玩家双击就能用」是隐患；
/// 2. 实测 .NET 10 的 <c>HttpListener.Start()</c> 在部分 Windows 环境会在
///    <c>SetupV2Config()</c> 抛 <c>HttpListenerException(6)：句柄无效</c>，
///    纯 .NET 10 空程序即可复现，与本项目代码无关，但会让界面无法打开。
///
/// 本实现只支持界面实际用到的子集：GET/POST、路径与查询串、请求体、
/// 固定长度响应，且一律 <c>Connection: close</c>（界面请求量很小，够用）。
/// </summary>
internal sealed class MiniHttpServer : IDisposable {
	/// <summary>一次请求的解析结果。</summary>
	internal sealed record Request(
		string Method,
		string Path,
		IReadOnlyDictionary<string, string> Query,
		string Body
	) {
		/// <summary>取查询参数；缺失时返回 null。</summary>
		internal string? QueryOrNull(string key) =>
			Query.TryGetValue(key, out string? value) ? value : null;

		/// <summary>取查询参数；缺失时返回空串。</summary>
		internal string QueryOrEmpty(string key) => QueryOrNull(key) ?? "";
	}

	/// <summary>一次响应的写出口。</summary>
	internal sealed class Response(Stream stream) {
		private bool written;

		/// <summary>HTTP 状态码，默认 200。</summary>
		internal int Status { get; set; } = 200;

		/// <summary>发送文本响应并结束本次请求。</summary>
		internal void WriteText(string text, string contentType) =>
			WriteBytes(Encoding.UTF8.GetBytes(text), contentType);

		/// <summary>发送字节响应并结束本次请求。</summary>
		internal void WriteBytes(byte[] body, string contentType) {
			if (written) {
				return;
			}

			written = true;

			StringBuilder head = new();
			head.Append("HTTP/1.1 ").Append(Status).Append(' ')
				.Append(StatusText(Status)).Append("\r\n");
			head.Append("Content-Type: ").Append(contentType).Append("\r\n");
			head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
			// 每次请求一个连接，省掉 keep-alive 的状态管理
			head.Append("Connection: close\r\n");
			head.Append("Cache-Control: no-store\r\n");
			head.Append("\r\n");

			byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
			stream.Write(headBytes, 0, headBytes.Length);
			stream.Write(body, 0, body.Length);
			stream.Flush();
		}

		private static string StatusText(int status) => status switch {
			200 => "OK",
			400 => "Bad Request",
			404 => "Not Found",
			500 => "Internal Server Error",
			_ => "OK",
		};
	}

	private readonly TcpListener listener;
	private readonly CancellationTokenSource cts = new();
	private readonly Action<Request, Response> handler;

	internal MiniHttpServer(int port, Action<Request, Response> handler) {
		this.handler = handler;
		listener = new TcpListener(IPAddress.Loopback, port);
	}

	/// <summary>实际监听的端口（端口传 0 时由系统分配）。</summary>
	internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

	/// <summary>开始监听；立即返回，接受连接在后台进行。</summary>
	internal void Start() {
		listener.Start();
		_ = Task.Run(AcceptLoopAsync);
	}

	private async Task AcceptLoopAsync() {
		while (!cts.IsCancellationRequested) {
			TcpClient client;
			try {
				client = await listener.AcceptTcpClientAsync(cts.Token);
			} catch (OperationCanceledException) {
				break;
			} catch (ObjectDisposedException) {
				break;
			} catch (SocketException) {
				// 单个连接异常不应终止整个服务
				continue;
			}

			_ = Task.Run(() => ServeAsync(client));
		}
	}

	private async Task ServeAsync(TcpClient client) {
		using (client) {
			try {
				client.NoDelay = true;
				NetworkStream stream = client.GetStream();
				Request? request = await ReadRequestAsync(stream);
				if (request is null) {
					return;
				}

				Response response = new(stream);
				handler(request, response);
			} catch {
				// 客户端提前断开属正常情况，静默忽略
			}
		}
	}

	/// <summary>读取并解析一个请求；报文不完整或非法时返回 null。</summary>
	private static async Task<Request?> ReadRequestAsync(NetworkStream stream) {
		// 先读到空行为止（请求头结束标记）
		MemoryStream headBuffer = new();
		byte[] one = new byte[1];
		int consecutiveNewlines = 0;

		while (headBuffer.Length < 64 * 1024) {
			int read = await stream.ReadAsync(one);
			if (read == 0) {
				return null;
			}

			headBuffer.WriteByte(one[0]);

			// 识别 \r\n\r\n 或 \n\n
			consecutiveNewlines = one[0] switch {
				(byte)'\n' => consecutiveNewlines + 1,
				(byte)'\r' => consecutiveNewlines,
				_ => 0,
			};

			if (consecutiveNewlines >= 2) {
				break;
			}
		}

		string head = Encoding.UTF8.GetString(headBuffer.ToArray());
		string[] lines = head.Split('\n');
		if (lines.Length == 0) {
			return null;
		}

		string[] requestLine = lines[0].Trim().Split(' ');
		if (requestLine.Length < 2) {
			return null;
		}

		string method = requestLine[0].ToUpperInvariant();
		string target = requestLine[1];

		string path = target;
		Dictionary<string, string> query = [];
		int qmark = target.IndexOf('?');
		if (qmark >= 0) {
			path = target[..qmark];
			foreach (string pair in target[(qmark + 1)..].Split('&')) {
				if (pair.Length == 0) {
					continue;
				}

				int eq = pair.IndexOf('=');
				if (eq < 0) {
					query[Decode(pair)] = "";
				} else {
					query[Decode(pair[..eq])] = Decode(pair[(eq + 1)..]);
				}
			}
		}

		// 请求头：找 Content-Length
		int contentLength = 0;
		foreach (string line in lines.Skip(1)) {
			int colon = line.IndexOf(':');
			if (colon <= 0) {
				continue;
			}

			if (line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
				&& int.TryParse(line[(colon + 1)..].Trim(), out int len)) {
				contentLength = len;
			}
		}

		string body = "";
		if (contentLength > 0) {
			byte[] bodyBuffer = new byte[contentLength];
			int offset = 0;
			while (offset < contentLength) {
				int read = await stream.ReadAsync(bodyBuffer.AsMemory(offset, contentLength - offset));
				if (read == 0) {
					break;
				}

				offset += read;
			}

			body = Encoding.UTF8.GetString(bodyBuffer, 0, offset);
		}

		return new Request(method, path, query, body);
	}

	private static string Decode(string value) {
		try {
			return Uri.UnescapeDataString(value.Replace('+', ' '));
		} catch {
			return value;
		}
	}

	public void Dispose() {
		cts.Cancel();
		try {
			listener.Stop();
		} catch {
			// 停止失败不影响退出
		}

		cts.Dispose();
	}
}