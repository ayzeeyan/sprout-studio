using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using SproutStudio.Core;

namespace SproutStudio.Interop;

public sealed partial class NativeSproutEngineClient : IEngineClient
{
    public const int SupportedApiVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public Task<EngineResponse> ExecuteAsync(
        EngineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Execute(request), cancellationToken);
    }

    public static EngineResponse ParseResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        EngineResponse response = JsonSerializer.Deserialize<EngineResponse>(json, JsonOptions)
            ?? throw new InvalidDataException("The Sprout engine returned an empty response.");
        if (response.ApiVersion != SupportedApiVersion)
        {
            throw new InvalidDataException(
                $"Unsupported Sprout API version {response.ApiVersion}; expected {SupportedApiVersion}.");
        }

        return response;
    }

    private static EngineResponse Execute(EngineRequest request)
    {
        byte[] name = Encoding.UTF8.GetBytes(request.SourceName);
        byte[] source = Encoding.UTF8.GetBytes(request.Source);
        NativeBuffer buffer = NativeMethods.SproutExecute(
            (uint)request.Operation,
            (uint)request.Profile,
            name,
            (nuint)name.Length,
            source,
            (nuint)source.Length);
        try
        {
            int length = checked((int)buffer.Length);
            byte[] response = new byte[length];
            if (length > 0)
            {
                Marshal.Copy(buffer.Data, response, 0, length);
            }
            return ParseResponse(Encoding.UTF8.GetString(response));
        }
        finally
        {
            NativeMethods.SproutBufferFree(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeBuffer
    {
        public readonly IntPtr Data;
        public readonly nuint Length;
        public readonly nuint Capacity;
    }

    private static partial class NativeMethods
    {
        private const string LibraryName = "sprout_ffi";

        [LibraryImport(LibraryName, EntryPoint = "sprout_execute")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutExecute(
            uint operation,
            uint profile,
            byte[] sourceName,
            nuint sourceNameLength,
            byte[] source,
            nuint sourceLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_buffer_free")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void SproutBufferFree(NativeBuffer buffer);
    }
}
