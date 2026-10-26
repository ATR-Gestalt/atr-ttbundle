using System.Text.RegularExpressions;

namespace StlTechRelGen.Utils;

internal static partial class GlobalInstances {
	public static class Yaml {
		internal static YamlDeserializer Deserializer { get; } = new();

		// PDX localization files always use double-quoted strings with
		// Unix-style newlines. The serializer must match this format
		// exactly; otherwise the game may fail to parse the output YAML.
		internal static IYamlSerializer Serializer { get; } =
			new YamlSerializerBuilder()
				.WithDefaultScalarStyle(
					YamlDotNet.Core.ScalarStyle.DoubleQuoted
				)
				.WithNewLine("\n")
				.Build();
	}

	internal static Encoding Utf8Bom { get; } = new UTF8Encoding(
		encoderShouldEmitUTF8Identifier: true,
		throwOnInvalidBytes: true
	);

	internal static HttpClient Client { get; } = new();

	// Matches $tech_id_desc_suffix$ references embedded in localization text.
	// Named groups:
	//   key    — full loc key inside $...$ (e.g. "tech_foo_desc_bar")
	//   id     — tech identifier part (e.g. "tech_foo")
	//   suffix — optional auth suffix (e.g. "_bar"); may not be present.
	//            The .+? is non-greedy to match each reference individually.
	[GeneratedRegex(
		@"\$(?<key>(?<id>.+?)_desc(?<suffix>_.+?)?)\$",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
	)]
	internal static partial Regex RegexLocReference();

	internal static readonly FileStreamOptions FileWriteOptions = new() {
		Mode = FileMode.Create,
		Access = FileAccess.Write,
		Share = FileShare.None,
		Options = FileOptions.SequentialScan
	};

	static GlobalInstances() =>
		Client.DefaultRequestHeaders.UserAgent.Add(new(
			nameof(StlTechRelGen),
			GetProgramVersion()
		));

	public static string GetProgramVersion() => Assembly
		.GetExecutingAssembly()
		.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
		.InformationalVersion!;
}
