# Импорт дообученной модели в Ollama (контейнер opsmind-ollama) и оценка на тех же наборах.
# Использование: .\py-lab\finetune\import-to-ollama.ps1 -Gguf C:\Downloads\unsloth.Q4_K_M.gguf
param(
    [Parameter(Mandatory)] [string] $Gguf,
    [string] $Name = "opsmind-classifier"
)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

docker exec opsmind-ollama mkdir -p /models/opsmind
docker cp $Gguf opsmind-ollama:/models/opsmind/opsmind-classifier.gguf
docker cp (Join-Path $here "Modelfile") opsmind-ollama:/models/opsmind/Modelfile
docker exec -w /models/opsmind opsmind-ollama ollama create $Name -f Modelfile
docker exec opsmind-ollama ollama list

$root = Resolve-Path (Join-Path $here "..\..")
foreach ($set in "test", "hard") {
    dotnet run --project (Join-Path $root "tools\OpsMind.ClassifierEval") -- --model $Name --mode bare --set $set --limit 150
}
