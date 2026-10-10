# Components

ZeroVoice application code is an independent MIT-licensed implementation. No Meltype source code is incorporated. Meltype (GPL-3.0-or-later) was consulted for design context: https://github.com/yksr-melt/Meltype .

- Avalonia 12.1.4: MIT; https://github.com/AvaloniaUI/Avalonia
- .NET 10 runtime: MIT and third-party notices bundled with the runtime; https://github.com/dotnet/runtime
- llama.cpp b11552: MIT; https://github.com/ggml-org/llama.cpp/tree/b11552
- Qwen3-4B-Instruct-2507: Apache-2.0; https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507 . GGUF Q4_K_M quantization by bartowski: https://huggingface.co/bartowski/Qwen_Qwen3-4B-Instruct-2507-GGUF/tree/ae44f08e1392f39c0e474af10c3ff8355c8b6688 . Model weights are data; no model repository Python code is executed.
- Skia/HarfBuzz and platform dependencies: their licenses remain applicable; resolved package versions and hashes are recorded in `src/ZeroVoice.Desktop/packages.lock.json`.
- Handy 0.9.8 (installed separately): MIT; https://github.com/cjpais/Handy

The distribution includes component license texts in `licenses/`. Model and runtime download hashes are checked before use. The MIT license for ZeroVoice does not replace these component licenses.
