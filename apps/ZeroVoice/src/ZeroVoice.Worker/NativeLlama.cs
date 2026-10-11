using System.Runtime.InteropServices;

namespace ZeroVoice.Worker;

// ABI pinned to llama.cpp b11552, include/llama.h. Only the owned worker loads it.
internal static class Llama
{
    const string Lib = "zerovoice-llama";
    [StructLayout(LayoutKind.Sequential)] internal struct ModelParams
    {
        public nint Devices, Overrides;
        public int GpuLayers, SplitMode, LoadMode, LazyMode, MainGpu;
        public nint TensorSplit, Progress, ProgressData, KvOverrides;
        public byte VocabOnly, CheckTensors, ExtraBuffers, NoHost, NoAlloc, LoadMtp;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct ContextParams
    {
        public uint Context, Batch, MicroBatch, Sequences, RollbackSnapshots, MaxOutputs, MaxOutputsPerSeq;
        public int Threads, BatchThreads, ContextType, RopeType, PoolingType, AttentionType, FlashAttention;
        public float RopeBase, RopeScale, YarnExt, YarnAttn, YarnBetaFast, YarnBetaSlow;
        public uint YarnOriginal;
        public float Defrag;
        public nint Eval, EvalData;
        public int TypeK, TypeV;
        public nuint MoeCache;
        public nint Abort, AbortData;
        public byte Embeddings, OffloadKqv, NoPerf, OpOffload, SwaFull, KvUnified;
        public nint Samplers;
        public nuint SamplerCount;
        public nint OtherContext;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Batch
    { public int Count; public nint Tokens, Embeddings, Positions, SequenceCounts, SequenceIds, Logits; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LogCallback(int level, nint text, nint data);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_log_set(LogCallback callback, nint data);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_backend_init();
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern ModelParams llama_model_default_params();
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern ContextParams llama_context_default_params();
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_model_load_from_file([MarshalAs(UnmanagedType.LPUTF8Str)]string path, ModelParams parameters);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_init_from_model(nint model, ContextParams parameters);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_model_get_vocab(nint model);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int llama_tokenize(nint vocabulary, byte[] text, int bytes, [Out]int[] tokens, int capacity, byte addSpecial, byte parseSpecial);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int llama_token_to_piece(nint vocabulary, int token, [Out]byte[] piece, int capacity, int strip, byte special);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern byte llama_vocab_is_eog(nint vocabulary, int token);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int llama_decode(nint context, Batch batch);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_get_memory(nint context);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_memory_clear(nint memory, byte clearData);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nuint llama_state_get_size(nint context);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nuint llama_state_get_data(nint context, [Out]byte[] data, nuint size);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nuint llama_state_set_data(nint context, byte[] data, nuint size);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_sampler_chain_init(byte noPerf);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_sampler_chain_add(nint chain, nint sampler);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_sampler_init_grammar(nint vocabulary,[MarshalAs(UnmanagedType.LPUTF8Str)]string grammar,[MarshalAs(UnmanagedType.LPUTF8Str)]string root);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern nint llama_sampler_init_greedy();
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern int llama_sampler_sample(nint sampler, nint context, int index);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_sampler_accept(nint sampler, int token);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_sampler_free(nint sampler);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_free(nint context);
    [DllImport(Lib, CallingConvention=CallingConvention.Cdecl)] internal static extern void llama_model_free(nint model);
}
