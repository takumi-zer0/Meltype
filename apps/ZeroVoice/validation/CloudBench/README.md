# Synthetic OpenAI comparison

This explicitly invoked benchmark sends only the supplied cases to OpenAI. It does not read audio, Handy history, application context or the clipboard. It is separate from the local-only Handy correction build.

Requires .NET 10 and a separately provisioned `OPENAI_API_KEY` in a local env file outside this source tree. Never commit or archive that file. The key is read in memory; only its file path is an argument. Redirects are disabled, TLS verification remains enabled, and the endpoint is fixed to `https://api.openai.com/v1/responses`.

Run from the ZeroVoice source root, supplying the absolute path of the approved env file:

```powershell
dotnet run --project validation/CloudBench -c Release -- validation/handy-comparison/cases.json results.json PATH_TO_PRIVATE_ENV_FILE validation/CloudBench/instructions.txt none
```

The fixed instructions match `ResidentModel.Instructions` at the measured revision. `none` and `low` are supported; the published comparison used `none`. The model is `gpt-6-luna`, storage is disabled, and output uses a strict one-field JSON Schema. Core preprocessing and output guards match the local benchmark.

Requests are sequential with a five-second gap excluded from response latency. A rate limit or other API error stops the run, saves completed cases, and exits nonzero. Do not aggregate duplicate reruns as new samples. Results contain the supplied synthetic input and model output plus token usage. Do not use private real dictation unless you intend to send and save it. `store:false` does not remove all provider retention.

The original comparison hit HTTP 429 after 13 completed requests before pacing was added. The remaining 11 cases completed with pacing. See the adjacent report for quality limits, timings and estimated costs.
