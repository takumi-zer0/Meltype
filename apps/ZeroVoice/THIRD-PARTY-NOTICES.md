# Components

ZeroVoice application code is an independent MIT-licensed implementation. No Meltype source code is incorporated. Meltype (GPL-3.0-or-later) was consulted for design context: https://github.com/yksr-melt/Meltype .

- Avalonia 12.1.4: MIT; https://github.com/AvaloniaUI/Avalonia
- .NET 10 runtime: MIT and third-party notices bundled with the runtime; https://github.com/dotnet/runtime
- llama.cpp b11552: MIT; https://github.com/ggml-org/llama.cpp/tree/b11552
- Qwen3-4B-Instruct-2507: Apache-2.0; https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507 . GGUF Q4_K_M quantization by bartowski: https://huggingface.co/bartowski/Qwen_Qwen3-4B-Instruct-2507-GGUF/tree/ae44f08e1392f39c0e474af10c3ff8355c8b6688 . Model weights are data; no model repository Python code is executed.
- Skia/HarfBuzz and platform dependencies: their licenses remain applicable; resolved package versions and hashes are recorded in `src/ZeroVoice.Desktop/packages.lock.json`.
- Handy 0.9.8: MIT; https://github.com/cjpais/Handy . The independent desktop build connects to a separately installed Handy. The integrated build applies `handy/Handy.patch` to upstream commit `48a3d8e9ee456c1c6a3b7223779271c457890b4d` and includes Handy.
- Gemma 4 E2B IT QAT Q4_0 (integrated build): Apache-2.0; https://huggingface.co/google/gemma-4-E2B-it-qat-q4_0-gguf/tree/675cff42a74c774d6cb76f76d8eacb49b48c9b93 . The model download is verified against SHA-256 `fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634`. Model repository Python code is not executed.

The distribution includes component license texts in `licenses/`. Model and runtime download hashes are checked before use. The MIT license for ZeroVoice does not replace these component licenses.
