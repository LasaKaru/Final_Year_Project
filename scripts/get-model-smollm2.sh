#!/usr/bin/env bash
# Provision the tiny local model used for BolaLens hybrid-mode plumbing/IPD demo:
# SmolLM2-135M-Instruct (Apache-2.0) converted to ONNX Runtime GenAI, int4, CPU (~86 MB).
# Needs: python3 (3.10+), internet access to huggingface.co, pypi.org and download.pytorch.org — ONCE.
# After this, BolaLens runs fully offline.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${1:-$ROOT/models/smollm2-135m-instruct-int4}"
WORK="${BOLALENS_MODEL_WORK:-$ROOT/.model-work}"
REPO="HuggingFaceTB/SmolLM2-135M-Instruct"
REV="main"
mkdir -p "$WORK/src"

python3 -m venv "$WORK/venv"
# shellcheck disable=SC1091
. "$WORK/venv/bin/activate"
pip install -q --upgrade pip
pip install -q torch --index-url https://download.pytorch.org/whl/cpu
pip install -q "onnxruntime-genai==0.20.0" onnx onnxruntime transformers onnx_ir

for f in config.json generation_config.json tokenizer.json tokenizer_config.json special_tokens_map.json vocab.json merges.txt model.safetensors; do
  [ -s "$WORK/src/$f" ] || curl -sSL --fail -o "$WORK/src/$f" "https://huggingface.co/$REPO/resolve/$REV/$f"
done
echo "source weights sha256: $(sha256sum "$WORK/src/model.safetensors" | cut -d' ' -f1)"

python -m onnxruntime_genai.models.builder -i "$WORK/src" -o "$OUT" -p int4 -e cpu -c "$WORK/cache" \
  --extra_options hf_token=false shared_embeddings=true op_types_to_quantize=MatMul/Gather accuracy_level=4

echo "--- $OUT"
du -sh "$OUT"
(cd "$OUT" && sha256sum ./* | sort -k2)
echo "Done. Try: dotnet run --project src/BolaLens.Cli -- scan fixtures/BolaLab/BolaLab.sln --mode hybrid --model $OUT"
