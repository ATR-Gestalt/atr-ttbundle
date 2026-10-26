using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StlTechRelGen;

/// <summary>
/// 本地 Web UI：起一个只监听 127.0.0.1 的 HTTP 服务，浏览器打开即用。
///
/// 选择 Web 而不是原生窗口，是为了 Windows 与 Linux 共用一套界面，
/// 也不给「免安装单文件」增加 GUI 框架依赖。
/// </summary>
internal static class UiServer {
	private static readonly JsonSerializerOptions JsonOpts = new() {
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = false,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	private static readonly Lock Gate = new();
	private static readonly List<string> Logs = [];
	private static string state = "idle";
	private static string? error;
	private static object? result;
	private static Config config = NewDefaultConfig();
	private static CancellationTokenSource? cts;
	private static string configPath = "";

	private static Config NewDefaultConfig() => new() {
		Game = new() { GamePath = "", DocumentPath = "" },
		Update = new() { CheckUpdate = false },
	};

	/// <summary>启动服务，返回可访问的 URL。</summary>
	public static string Start(int port = 0) {
		configPath = Utils.Constants.ConfigPath;
		Config? loaded = Config.Load();
		if (loaded is not null) {
			config = loaded;
		}

		HttpListener listener = new();
		int actualPort = port > 0 ? port : FreePort();
		string prefix = $"http://127.0.0.1:{actualPort}/";
		listener.Prefixes.Add(prefix);
		listener.Start();

		_ = Task.Run(async () => {
			while (listener.IsListening) {
				HttpListenerContext ctx;
				try {
					ctx = await listener.GetContextAsync();
				} catch {
					break;
				}

				_ = Task.Run(() => Handle(ctx));
			}
		});

		return prefix;
	}

	public static void OpenBrowser(string url) {
		try {
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		} catch {
			// 无桌面环境时忽略，用户可手动打开
		}
	}

	private static int FreePort() {
		System.Net.Sockets.TcpListener l = new(IPAddress.Loopback, 0);
		l.Start();
		int port = ((IPEndPoint)l.LocalEndpoint).Port;
		l.Stop();
		return port;
	}

	private static void Handle(HttpListenerContext ctx) {
		try {
			string path = ctx.Request.Url?.AbsolutePath ?? "/";
			string method = ctx.Request.HttpMethod;

			if (method == "GET" && (path == "/" || path == "/index.html")) {
				WriteText(ctx, LoadHtml(), "text/html; charset=utf-8");
			} else if (method == "GET" && path == "/api/config") {
				WriteJson(ctx, ConfigDto());
			} else if (method == "POST" && path == "/api/config") {
				ApplyConfig(ReadBody(ctx));
				WriteJson(ctx, new { message = "配置已保存" });
			} else if (method == "GET" && path == "/api/playsets") {
				// doc 参数来自界面当前输入，避免「刚填的路径不生效」
				string? doc = ctx.Request.QueryString["doc"];
				WriteJson(ctx, Safe(() => Generator.ListPlaysets(config, doc)) ?? []);
			} else if (method == "GET" && path == "/api/mods") {
				string playset = ctx.Request.QueryString["playset"] ?? "";
				string? doc = ctx.Request.QueryString["doc"];
				WriteJson(ctx, Safe(() => Generator.ListModsInPlayset(config, playset, doc)) ?? []);
			} else if (method == "GET" && path == "/api/browse") {
				WriteJson(ctx, Browse(ctx.Request.QueryString["path"]));
			} else if (method == "GET" && path == "/api/suggest") {
				WriteJson(ctx, Suggest());
			} else if (method == "POST" && path == "/api/generate") {
				StartGenerate();
				WriteJson(ctx, new { message = "已开始" });
			} else if (method == "GET" && path == "/api/status") {
				WriteJson(ctx, StatusDto());
			} else {
				ctx.Response.StatusCode = 404;
				WriteText(ctx, "not found", "text/plain; charset=utf-8");
			}
		} catch (Exception e) {
			try {
				ctx.Response.StatusCode = 500;
				WriteText(ctx, e.ToString(), "text/plain; charset=utf-8");
			} catch {
				// ignore
			}
		}
	}

	/// <summary>
	/// 目录浏览。无 path 参数时返回驱动器/根列表；有 path 时返回该目录的子目录。
	/// 只读，仅用于让用户在界面里点选文件夹（浏览器无法调用系统文件对话框）。
	/// </summary>
	private static object Browse(string? requested) {
		if (string.IsNullOrWhiteSpace(requested)) {
			List<string> roots;
			try {
				roots = [.. DriveInfo.GetDrives()
					.Where(d => {
						try { return d.IsReady; } catch { return false; }
					})
					.Select(d => d.RootDirectory.FullName)
					.OrderBy(i => i, StringComparer.Ordinal)];
			} catch {
				roots = OperatingSystem.IsWindows() ? [@"C:\"] : ["/"];
			}

			return new {
				path = "",
				parent = (string?)null,
				dirs = Array.Empty<object>(),
				roots,
			};
		}

		string dir;
		try {
			dir = Path.GetFullPath(requested);
		} catch {
			return new { path = requested, parent = (string?)null, dirs = Array.Empty<object>(), roots = Array.Empty<string>() };
		}

		List<object> dirs = [];
		if (Directory.Exists(dir)) {
			try {
				foreach (string sub in Directory.EnumerateDirectories(dir)) {
					string name;
					try {
						name = Path.GetFileName(sub);
					} catch {
						continue;
					}

					// 跳过隐藏项，减少干扰（但系统目录仍然可见）
					try {
						if (Path.GetFileName(sub).StartsWith('.')) {
							continue;
						}
					} catch {
						// ignore
					}

					dirs.Add(new { name, path = sub });
				}
			} catch {
				// 无权限读取时返回空列表，由界面提示
			}
		}

		dirs = [.. dirs.Cast<dynamic>()
			.OrderBy(i => (string)i.name, StringComparer.Ordinal)];

		string? parent;
		try {
			parent = Path.GetDirectoryName(dir);
		} catch {
			parent = null;
		}

		return new {
			path = dir,
			parent,
			dirs,
			roots = Array.Empty<string>(),
		};
	}

	/// <summary>返回常见路径候选，供界面「自动填入」按钮使用。</summary>
	private static object Suggest() {
		string? doc = Utils.Constants.DefaultDocumentPath;
		string? modDir = doc is null ? null : Path.Combine(doc, "mod");
		return new {
			gamePath = Safe(GameLocator.TryFind),
			documentPath = doc,
			modDir = modDir is not null && Directory.Exists(modDir) ? modDir : null,
		};
	}

	private static T Safe<T>(Func<T> func) {
		try {
			return func();
		} catch (Exception e) {
			// 把异常写进日志，界面上能看到，便于排查（比如启动器占用了数据库）
			lock (Gate) {
				Logs.Add("接口异常：" + e.Message);
			}

			return default!;
		}
	}

	private static string LoadHtml() {
		System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
		const string name = "StlTechRelGen.Resources.Ui.index.html";
		using Stream? s = asm.GetManifestResourceStream(name);
		if (s is null) {
			return "<h1>UI 资源缺失</h1><p>请重新编译。</p>";
		}

		using StreamReader r = new(s, Encoding.UTF8);
		return r.ReadToEnd();
	}

	private static string ReadBody(HttpListenerContext ctx) {
		using StreamReader r = new(ctx.Request.InputStream, Encoding.UTF8);
		return r.ReadToEnd();
	}

	private static void WriteJson(HttpListenerContext ctx, object data) =>
		WriteText(ctx, JsonSerializer.Serialize(data, JsonOpts), "application/json; charset=utf-8");

	private static void WriteText(HttpListenerContext ctx, string text, string contentType) {
		byte[] buf = Encoding.UTF8.GetBytes(text);
		ctx.Response.ContentType = contentType;
		ctx.Response.ContentLength64 = buf.Length;
		ctx.Response.OutputStream.Write(buf, 0, buf.Length);
		ctx.Response.OutputStream.Close();
	}

	private static object ConfigDto() => new {
		gamePath = config.Game.GamePath,
		documentPath = config.Game.DocumentPath,
		mode = config.Data.Mode,
		playset = !string.IsNullOrWhiteSpace(config.Data.Playset)
			? config.Data.Playset
			: config.Playset?.Name ?? "",
		sourceMod = config.Data.SourceMod,
		targetMod = ResolveTargetModText(),
		targetDir = ResolveTargetDirText(),
		filePrefix = config.Output.FilePrefix,
		writeFragments = config.Output.WriteFragments,
		languages = config.Output.Languages,
		sections = config.Sections.Order,
		tree = new {
			enabled = config.Tree.Enabled,
			direction = config.Tree.Direction,
			indent = config.Tree.IndentStyle,
			indentWidth = config.Tree.IndentWidth,
			maxLines = config.Tree.MaxLines,
			title = config.Tree.Title,
			truncateText = config.Tree.TruncateText,
			nodeTemplate = config.Tree.NodeTemplate,
			colorByArea = config.Tree.ColorByArea,
		},
		labels = new {
			enabled = config.Labels.Enabled,
			areaIcon = config.Labels.AreaIcon,
			areaName = config.Labels.AreaName,
			tier = config.Labels.Tier,
			tierZero = config.Labels.TierZero,
			category = config.Labels.Category,
			rare = config.Labels.Rare,
			dangerous = config.Labels.Dangerous,
			repeatable = config.Labels.Repeatable,
			cost = config.Labels.Cost,
			color = config.Labels.Color,
			delimiter = config.Labels.Delimiter,
		},
		lists = new {
			enabled = config.Lists.Enabled,
			requiresTitle = config.Lists.RequiresTitle ?? "",
			unlocksTitle = config.Lists.UnlocksTitle ?? "",
			oneOfTitle = config.Lists.OneOfTitle ?? "",
			maxItems = config.Lists.MaxItems,
			truncateText = config.Lists.TruncateText,
			showAreaIcon = config.Lists.ShowAreaIcon,
		},
		data = new {
			includeVanilla = config.Data.IncludeVanilla,
			excludeEventTechs = config.Data.ExcludeEventTechs,
			excludeGatewayTechs = config.Data.ExcludeGatewayTechs,
			excludeStartTechs = config.Data.ExcludeStartTechs,
		},
		advanced = new {
			checkUpdate = config.Update.CheckUpdate,
			overrideLanguage = config.OverrideLanguage ?? "",
			yesmen = config.Yesmen,
			suppressesCwtoolsErrors = config.SuppressesCWToolsErrors,
		},
	};

	private static string ResolveTargetModText() {
		if (!string.IsNullOrWhiteSpace(config.Output.TargetMod)) {
			return config.Output.TargetMod;
		}

		return string.Equals(config.Data.Mode, "playset", StringComparison.OrdinalIgnoreCase)
			? config.Playset?.Target ?? ""
			: "";
	}

	private static string ResolveTargetDirText() {
		if (!string.IsNullOrWhiteSpace(config.Output.TargetDir)) {
			return config.Output.TargetDir;
		}

		return string.Equals(config.Data.Mode, "playset", StringComparison.OrdinalIgnoreCase)
			? ""
			: config.Playset?.Target ?? "";
	}

	private static void ApplyConfig(string body) {
		using JsonDocument doc = JsonDocument.Parse(body);
		JsonElement root = doc.RootElement;

		config.Game.GamePath = Str(root, "gamePath") is { Length: > 0 } gp
			? gp
			: config.Game.GamePath;
		config.Game.DocumentPath = Str(root, "documentPath") is { Length: > 0 } dp
			? dp
			: Utils.Constants.DefaultDocumentPath ?? "";

		config.Data.Mode = string.Equals(Str(root, "mode"), "playset", StringComparison.OrdinalIgnoreCase)
			? "playset"
			: "vanilla";
		config.Data.Playset = Str(root, "playset") ?? "";
		config.Data.SourceMod = Str(root, "sourceMod") ?? "";
		config.Output.TargetMod = Str(root, "targetMod") ?? "";
		config.Output.TargetDir = Str(root, "targetDir") ?? "";
		// 旧 [playset] 段由新字段取代，保存时不再写出
		config.Playset = null;

		config.Output.FilePrefix = Str(root, "filePrefix") is { Length: > 0 } fp ? fp : "zz_techtree";
		config.Output.WriteFragments = Bool(root, "writeFragments") ?? config.Output.WriteFragments;

		if (root.TryGetProperty("languages", out JsonElement langs) && langs.ValueKind == JsonValueKind.Array) {
			config.Output.Languages = [.. langs.EnumerateArray()
				.Select(i => i.GetString() ?? "")
				.Where(i => i.Length > 0)];
		}

		if (root.TryGetProperty("sections", out JsonElement secs) && secs.ValueKind == JsonValueKind.Array) {
			List<string> order = [.. secs.EnumerateArray()
				.Select(i => i.GetString() ?? "")
				.Where(i => i.Length > 0)];
			if (order.Count > 0) {
				config.Sections.Order = order;
			}
		}

		if (root.TryGetProperty("data", out JsonElement data)) {
			config.Data.IncludeVanilla = Bool(data, "includeVanilla") ?? config.Data.IncludeVanilla;
			config.Data.ExcludeEventTechs =
				Bool(data, "excludeEventTechs") ?? config.Data.ExcludeEventTechs;
			config.Data.ExcludeGatewayTechs =
				Bool(data, "excludeGatewayTechs") ?? config.Data.ExcludeGatewayTechs;
			config.Data.ExcludeStartTechs =
				Bool(data, "excludeStartTechs") ?? config.Data.ExcludeStartTechs;
		}

		if (root.TryGetProperty("tree", out JsonElement tree)) {
			config.Tree.Enabled = Bool(tree, "enabled") ?? config.Tree.Enabled;
			config.Tree.Direction = Str(tree, "direction") ?? config.Tree.Direction;
			config.Tree.IndentStyle = Str(tree, "indent") ?? config.Tree.IndentStyle;
			config.Tree.IndentWidth = Int(tree, "indentWidth") ?? config.Tree.IndentWidth;
			config.Tree.MaxLines = Int(tree, "maxLines") ?? config.Tree.MaxLines;
			config.Tree.Title = Str(tree, "title") ?? config.Tree.Title;
			config.Tree.TruncateText = Str(tree, "truncateText") ?? config.Tree.TruncateText;
			config.Tree.NodeTemplate = Str(tree, "nodeTemplate") ?? config.Tree.NodeTemplate;
			config.Tree.ColorByArea = Bool(tree, "colorByArea") ?? config.Tree.ColorByArea;
		}

		if (root.TryGetProperty("labels", out JsonElement labels)) {
			config.Labels.Enabled = Bool(labels, "enabled") ?? config.Labels.Enabled;
			config.Labels.AreaIcon = Bool(labels, "areaIcon") ?? config.Labels.AreaIcon;
			config.Labels.AreaName = Bool(labels, "areaName") ?? config.Labels.AreaName;
			config.Labels.Tier = Bool(labels, "tier") ?? config.Labels.Tier;
			config.Labels.TierZero = Str(labels, "tierZero") ?? config.Labels.TierZero;
			config.Labels.Category = Bool(labels, "category") ?? config.Labels.Category;
			config.Labels.Rare = Bool(labels, "rare") ?? config.Labels.Rare;
			config.Labels.Dangerous = Bool(labels, "dangerous") ?? config.Labels.Dangerous;
			config.Labels.Repeatable = Bool(labels, "repeatable") ?? config.Labels.Repeatable;
			config.Labels.Cost = Bool(labels, "cost") ?? config.Labels.Cost;
			config.Labels.Color = Bool(labels, "color") ?? config.Labels.Color;
			// 分隔符可能就是一个空格，不能 Trim
			if (labels.TryGetProperty("delimiter", out JsonElement delim)
				&& delim.ValueKind == JsonValueKind.String) {
				config.Labels.Delimiter = delim.GetString() ?? config.Labels.Delimiter;
			}
		}

		if (root.TryGetProperty("lists", out JsonElement lists)) {
			config.Lists.Enabled = Bool(lists, "enabled") ?? config.Lists.Enabled;
			// 空串表示「用内置标题」；写 null 让生成时回退到 $techrel_*$ 片段
			config.Lists.RequiresTitle = Str(lists, "requiresTitle") is { Length: > 0 } rt
				? rt
				: null;
			config.Lists.UnlocksTitle = Str(lists, "unlocksTitle") is { Length: > 0 } ut
				? ut
				: null;
			config.Lists.OneOfTitle = Str(lists, "oneOfTitle") is { Length: > 0 } ot
				? ot
				: null;
			config.Lists.MaxItems = Int(lists, "maxItems") ?? config.Lists.MaxItems;
			config.Lists.TruncateText = Str(lists, "truncateText") ?? config.Lists.TruncateText;
			config.Lists.ShowAreaIcon = Bool(lists, "showAreaIcon") ?? config.Lists.ShowAreaIcon;
		}

		if (root.TryGetProperty("advanced", out JsonElement advanced)) {
			config.Update.CheckUpdate = Bool(advanced, "checkUpdate") ?? config.Update.CheckUpdate;
			config.OverrideLanguage = Str(advanced, "overrideLanguage");
			config.Yesmen = Bool(advanced, "yesmen") ?? config.Yesmen;
			config.SuppressesCWToolsErrors =
				Bool(advanced, "suppressesCwtoolsErrors") ?? config.SuppressesCWToolsErrors;
		}

		config.Normalize();
		config.Save();
	}

	private static string? Str(JsonElement e, string name) =>
		e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
			? v.GetString()?.Trim()
			: null;

	private static int? Int(JsonElement e, string name) =>
		e.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.Number
			? v.GetInt32()
			: null;

	private static bool? Bool(JsonElement e, string name) =>
		e.TryGetProperty(name, out JsonElement v)
			&& v.ValueKind is JsonValueKind.True or JsonValueKind.False
			? v.GetBoolean()
			: null;

	private static void StartGenerate() {
		lock (Gate) {
			if (state == "running") {
				return;
			}

			state = "running";
			error = null;
			result = null;
			Logs.Clear();
			cts = new CancellationTokenSource();
			CancellationToken token = cts.Token;

			Config snapshot = config;
			_ = Task.Run(async () => {
				try {
					Generator.Result r = await Generator.RunAsync(
						snapshot,
						msg => {
							lock (Gate) {
								Logs.Add(msg);
							}
						},
						token
					);

					lock (Gate) {
						state = "done";
						result = new {
							languageCount = r.LanguageCount,
							techCount = r.TechCount,
							relationCount = r.RelationCount,
							outputDirectory = r.OutputDirectory,
							fileCount = r.Files.Length,
							files = r.Files,
						};
						Logs.Add($"完成：{r.Files.Length} 个文件");
					}
				} catch (OperationCanceledException) {
					lock (Gate) {
						state = "error";
						error = "已取消";
					}
				} catch (Exception e) {
					lock (Gate) {
						state = "error";
						error = e.Message;
						Logs.Add("错误：" + e.Message);
					}
				}
			});
		}
	}

	private static object StatusDto() {
		lock (Gate) {
			return new {
				state,
				error,
				result,
				logs = Logs.ToArray(),
			};
		}
	}
}
