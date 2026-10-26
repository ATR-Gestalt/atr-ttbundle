using System.Text.Json.Serialization;

using Tomlyn.Serialization;

namespace StlTechRelGen.Utils;

[TomlSourceGenerationOptions(
	PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
	Converters = [typeof(CompositeFormatConverter)]
)]
[TomlSerializable(typeof(Config))]
[TomlSerializable(typeof(Lang))]
internal sealed partial class TomlContext : TomlSerializerContext {
	internal sealed class CompositeFormatConverter
		: TomlConverter<CompositeFormat> {
		public override CompositeFormat? Read(TomlReader reader) =>
			System.Text.CompositeFormat.Parse(reader.GetString());

		public override void Write(TomlWriter writer, CompositeFormat value) =>
			writer.WriteStringValue(value.Format);
	}
}
