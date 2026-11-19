using BolaLens.Inference;

namespace BolaLens.Inference.Tests;

public class ModelInfoTests
{
    private static string Folder(string genaiType, string? chatTemplate = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"bolalens-model-{Guid.NewGuid():N}", "my-model");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "genai_config.json"), "{\"model\":{\"type\":\"" + genaiType + "\"}}");
        if (chatTemplate is not null) File.WriteAllText(Path.Combine(dir, "chat_template.jinja"), chatTemplate);
        return dir;
    }

    [Fact]
    public void Detects_phi3_and_chatml_formats()
    {
        ModelInfo.Inspect(Folder("phi3")).ChatFormat.ShouldBe("phi3");
        ModelInfo.Inspect(Folder("llama", "{% for m in messages %}<|im_start|>{{m.role}}{% endfor %}")).ChatFormat.ShouldBe("chatml");
        ModelInfo.Inspect(Folder("qwen2")).ChatFormat.ShouldBe("chatml");
        var m = ModelInfo.Inspect(Folder("llama"), configuredFormat: "phi3");
        m.ChatFormat.ShouldBe("phi3");
        m.Id.ShouldBe("my-model");
        m.ModelType.ShouldBe("llama");
    }

    [Fact]
    public void Discovers_model_folders()
    {
        var dir = Folder("phi3");
        var root = Path.GetDirectoryName(dir)!;
        ModelInfo.Discover(root).ShouldHaveSingleItem().Id.ShouldBe("my-model");
        ModelInfo.Discover(Path.Combine(root, "missing")).ShouldBeEmpty();
    }
}
