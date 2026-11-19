# Local models (not committed)

Model files are git-ignored. Download them once, record their hashes here and in `docs/reproducibility.md`.

## Phi-3-mini-4k-instruct (ONNX, CPU int4) — default

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
