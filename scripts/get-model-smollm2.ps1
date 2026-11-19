# Windows version of get-model-smollm2.sh: SmolLM2-135M-Instruct -> ONNX Runtime GenAI int4 CPU (~86 MB).
param([string]$Out = "$PSScriptRoot\..\models\smollm2-135m-instruct-int4")
$ErrorActionPreference = "Stop"
$Work = "$PSScriptRoot\..\.model-work"
$Repo = "HuggingFaceTB/SmolLM2-135M-Instruct"
New-Item -ItemType Directory -Force -Path "$Work\src" | Out-Null
python -m venv "$Work\venv"
& "$Work\venv\Scripts\Activate.ps1"
pip install -q --upgrade pip
pip install -q torch --index-url https://download.pytorch.org/whl/cpu
pip install -q "onnxruntime-genai==0.20.0" onnx onnxruntime transformers onnx_ir
foreach ($f in "config.json","generation_config.json","tokenizer.json","tokenizer_config.json","special_tokens_map.json","vocab.json","merges.txt","model.safetensors") {
  if (-not (Test-Path "$Work\src\$f")) { Invoke-WebRequest "https://huggingface.co/$Repo/resolve/main/$f" -OutFile "$Work\src\$f" }
}
python -m onnxruntime_genai.models.builder -i "$Work\src" -o $Out -p int4 -e cpu -c "$Work\cache" `
  --extra_options hf_token=false shared_embeddings=true op_types_to_quantize=MatMul/Gather accuracy_level=4
Get-ChildItem $Out -File | Get-FileHash -Algorithm SHA256 | Format-Table Hash, Path
