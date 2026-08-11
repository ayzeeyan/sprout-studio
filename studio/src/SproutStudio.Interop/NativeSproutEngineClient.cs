using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SproutStudio.Core;
using SproutStudio.Editor;

namespace SproutStudio.Interop;

public sealed partial class NativeSproutEngineClient : IEngineClient, IProjectClient,
    ISproutLanguageService
{
    public const int SupportedApiVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public Task<EngineResponse> ExecuteAsync(
        EngineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => Execute(request), cancellationToken);
    }

    public Task<ProjectResponse> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        return Task.Run(() => LoadProject(projectPath), cancellationToken);
    }

    public Task<ProjectResponse> CreateAsync(
        string destination,
        string name,
        ProjectTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.Run(() => CreateProject(destination, name, template), cancellationToken);
    }

    public Task<LanguageServiceResponse> QueryAsync(
        LanguageServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => QueryLanguageService(request), cancellationToken);
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

    public static ProjectResponse ParseProjectResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        ProjectResponse response = JsonSerializer.Deserialize<ProjectResponse>(json, JsonOptions)
            ?? throw new InvalidDataException("The Sprout project service returned an empty response.");
        if (response.ApiVersion != SupportedApiVersion)
        {
            throw new InvalidDataException(
                $"Unsupported Sprout API version {response.ApiVersion}; expected {SupportedApiVersion}.");
        }

        return response;
    }

    public static LanguageServiceResponse ParseLanguageServiceResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        LanguageServiceResponse response = JsonSerializer.Deserialize<LanguageServiceResponse>(
                json,
                JsonOptions)
            ?? throw new InvalidDataException(
                "The Sprout language service returned an empty response.");
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
        NativeBuffer buffer;
        if (request.ProjectRoot is null)
        {
            buffer = NativeMethods.SproutExecute(
                (uint)request.Operation,
                (uint)request.Profile,
                name,
                (nuint)name.Length,
                source,
                (nuint)source.Length);
        }
        else
        {
            byte[] projectRoot = Encoding.UTF8.GetBytes(request.ProjectRoot);
            buffer = NativeMethods.SproutExecuteProject(
                (uint)request.Operation,
                (uint)request.Profile,
                projectRoot,
                (nuint)projectRoot.Length,
                name,
                (nuint)name.Length,
                source,
                (nuint)source.Length);
        }
        return ParseResponse(ReadBuffer(buffer));
    }

    private static ProjectResponse LoadProject(string projectPath)
    {
        byte[] path = Encoding.UTF8.GetBytes(projectPath);
        NativeBuffer buffer = NativeMethods.SproutProjectLoad(path, (nuint)path.Length);
        return ParseProjectResponse(ReadBuffer(buffer));
    }

    private static ProjectResponse CreateProject(
        string destination,
        string name,
        ProjectTemplate template)
    {
        byte[] destinationBytes = Encoding.UTF8.GetBytes(destination);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] templateBytes = Encoding.UTF8.GetBytes(template.ToString().ToLowerInvariant());
        NativeBuffer buffer = NativeMethods.SproutProjectCreate(
            destinationBytes,
            (nuint)destinationBytes.Length,
            nameBytes,
            (nuint)nameBytes.Length,
            templateBytes,
            (nuint)templateBytes.Length);
        return ParseProjectResponse(ReadBuffer(buffer));
    }

    private static LanguageServiceResponse QueryLanguageService(LanguageServiceRequest request)
    {
        byte[] name = Encoding.UTF8.GetBytes(request.SourceName);
        byte[] source = Encoding.UTF8.GetBytes(request.Source);
        int cursorIndex = Math.Clamp(request.CursorUtf16Index, 0, request.Source.Length);
        nuint cursorByteOffset = checked((nuint)Encoding.UTF8.GetByteCount(
            request.Source.AsSpan(0, cursorIndex)));
        NativeBuffer buffer = NativeMethods.SproutLanguageService(
            (uint)request.Operation,
            (uint)request.Profile,
            cursorByteOffset,
            name,
            (nuint)name.Length,
            source,
            (nuint)source.Length);
        return ParseLanguageServiceResponse(ReadBuffer(buffer));
    }

    private static string ReadBuffer(NativeBuffer buffer)
    {
        try
        {
            int length = checked((int)buffer.Length);
            byte[] response = new byte[length];
            if (length > 0)
            {
                Marshal.Copy(buffer.Data, response, 0, length);
            }
            return Encoding.UTF8.GetString(response);
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

        [LibraryImport(LibraryName, EntryPoint = "sprout_execute_project")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutExecuteProject(
            uint operation,
            uint profile,
            byte[] projectRoot,
            nuint projectRootLength,
            byte[] sourceName,
            nuint sourceNameLength,
            byte[] source,
            nuint sourceLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_project_load")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutProjectLoad(
            byte[] projectPath,
            nuint projectPathLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_project_create")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutProjectCreate(
            byte[] destination,
            nuint destinationLength,
            byte[] name,
            nuint nameLength,
            byte[] template,
            nuint templateLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_language_service")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutLanguageService(
            uint operation,
            uint profile,
            nuint cursor,
            byte[] sourceName,
            nuint sourceNameLength,
            byte[] source,
            nuint sourceLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_buffer_free")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void SproutBufferFree(NativeBuffer buffer);
    }
}
