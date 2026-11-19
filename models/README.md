# Local models (not committed)

Model files are git-ignored. Provision them once, record their hashes here and in `docs/reproducibility.md`.
`bolalens models` lists what is installed; the UI offers the same list.

## SmolLM2-135M-Instruct (ONNX Runtime GenAI, int4, CPU) — tiny model for plumbing / demo (86 MB)

```bash
scripts/get-model-smollm2.sh          # Windows: scripts/get-model-smollm2.ps1
```
Downloads `HuggingFaceTB/SmolLM2-135M-Instruct` (Apache-2.0) and converts it with the ONNX Runtime GenAI 0.20 model builder
(`-p int4 -e cpu`, `shared_embeddings=true op_types_to_quantize=MatMul/Gather accuracy_level=4`). The conversion is
reproducible — a fresh run produced byte-identical files:

| File | SHA-256 | Size |
|---|---|---|
| model.onnx.data | cc0a659144f9eff96c3c0335a5cf27d5e32b2eb7b9daa83f312360a9502e389c | 86.3 MB |
| model.onnx | 883b1491c1f039e8278d864ec019f36e28deee14f2333b02eaf6443e20065962 | 215 KB |
| genai_config.json | 9eaf4c580112da4d0dc5f2e9ca4bc7b8159d8a48da19362110c71f5323104187 | 1.5 KB |
| tokenizer.json | bf346d64f6f0fbcefb4c1b6928a98241467dff36c6fbae5fe1785c4ff90667f4 | 3.5 MB |
| tokenizer_config.json | 47b4a92de79d133d06b4cec56e9b8226d2be0b62936209577181d5a76444a08a | 452 B |
| chat_template.jinja | 872be49dbb638044ad01b60388f48d469ff2980e5f0dccdc22ec907db54d0788 | 368 B |

Source weights `model.safetensors` SHA-256 `5af571cbf074e6d21a03528d2330792e532ca608f24ac70a143f6b369968ab8c` (downloaded 2026-11-19).
Chat format: ChatML (auto-detected). **Finding:** it fails the calibration gate (answers "vulnerable" for everything) and is
therefore advisory only — it proves the pipeline end to end, not detection quality. Use Phi-3-mini (below) or a 0.5–1.5B
coder model for research results.

## Phi-3-mini-4k-instruct (ONNX, CPU int4) — research model (~2.7 GB)

```bash
python -m pip install -U "huggingface_hub[cli]"
hf download microsoft/Phi-3-mini-4k-instruct-onnx \
  --include "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/*" \
  --local-dir models/phi3-mini-4k
```

The folder `models/phi3-mini-4k/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4` (must contain
`genai_config.json`) is the default `inference.modelPath` in `bolalens.json`.

Record hashes (NFR05):

```bash
find models/phi3-mini-4k -type f -exec sha256sum {} \; | sort -k2
```

| File | SHA-256 | Size |
|---|---|---|
| _(fill in after download)_ | | |

* Model card: https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx — licence MIT — downloaded on: _(date)_
* Each report stores `run.modelSha256` = `manifest:<sha256>` over `genai_config.json`, the `*.onnx`
  graph files and the sizes of the external-data files (full-file hashing of GBs on every scan is too slow);
  the full per-file hashes above are the reference.

## Run hybrid mode

```bash
dotnet run --project src/BolaLens.Cli -c Release -- scan fixtures/BolaLab/BolaLab.sln --mode hybrid --out eval/runs/<id>/report.json
BOLALENS_MODEL_PATH=models/phi3-mini-4k/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4 dotnet test --filter "Category=RequiresModel"
```

Offline proof (NFR01): after download and `dotnet restore`, disable the network and run the hybrid scan; record it.

## Later candidates (S8)
`microsoft/Phi-4-mini-instruct-onnx`, Qwen2.5-Coder-1.5B/3B-Instruct exported with
`python -m onnxruntime_genai.models.builder -m <hf-id> -o <out> -p int4 -e cpu`. Check each licence first.
