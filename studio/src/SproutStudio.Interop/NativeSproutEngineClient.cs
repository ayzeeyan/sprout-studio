using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SproutStudio.Core;
using SproutStudio.Editor;

namespace SproutStudio.Interop;

public sealed partial class NativeSproutEngineClient : IEngineClient, IEngineInformationClient,
    IProjectClient, IProjectAdvancedClient, ISproutLanguageService, IEngineAstClient
{
    public const int SupportedApiVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private static readonly Lazy<EngineInfo> CompatibleEngine = new(
        LoadAndValidateEngine,
        LazyThreadSafetyMode.ExecutionAndPublication);

    static NativeSproutEngineClient()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(NativeSproutEngineClient).Assembly,
            ResolveSproutLibrary);
    }

    public Task<EngineInfo> GetInfoAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => CompatibleEngine.Value, cancellationToken);

    public Task<string> GetAstJsonAsync(string source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            NativeBuffer buffer = NativeMethods.SproutGetAstJson(source);
            return ReadBuffer(buffer);
        }, cancellationToken);
    }

    public Task<EngineResponse> ExecuteAsync(
        EngineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            return Execute(request);
        }, cancellationToken);
    }

    public Task<ProjectResponse> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            return LoadProject(projectPath);
        }, cancellationToken);
    }

    public Task<ProjectResponse> CreateAsync(
        string destination,
        string name,
        ProjectTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            return CreateProject(destination, name, template);
        }, cancellationToken);
    }

    public Task<LanguageServiceResponse> QueryAsync(
        LanguageServiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            return QueryLanguageService(request);
        }, cancellationToken);
    }

    public Task<ProjectResponse> UpdateDependencyAsync(
        string projectPath,
        ProjectDependencyKind kind,
        string name,
        string? requirement,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Task.Run(
            () =>
            {
                _ = CompatibleEngine.Value;
                return UpdateDependency(projectPath, kind, name, requirement);
            },
            cancellationToken);
    }

    public Task<BuildResponse> BuildAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        return Task.Run(() =>
        {
            _ = CompatibleEngine.Value;
            return BuildProject(projectPath);
        }, cancellationToken);
    }

    public static EngineInfo ParseEngineInfo(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<EngineInfo>(json, JsonOptions)
            ?? throw new InvalidDataException("The Sprout engine returned empty metadata.");
    }

    private static EngineInfo LoadAndValidateEngine()
    {
        EngineInfo info = ParseEngineInfo(ReadBuffer(NativeMethods.SproutEngineInfo()));
        if (info.AbiVersion != SupportedApiVersion)
        {
            throw new InvalidDataException(
                $"Sprout engine ABI {info.AbiVersion} is incompatible with Studio ABI {SupportedApiVersion}.");
        }
        if (!string.Equals(info.Architecture, "x86_64", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadImageFormatException(
                $"Sprout Studio requires an x64 engine; the bundled engine reports {info.Architecture}.");
        }
#if DEBUG
        const string expectedConfiguration = "debug";
#else
        const string expectedConfiguration = "release";
#endif
        if (!string.Equals(
            info.BuildConfiguration,
            expectedConfiguration,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Sprout Studio {expectedConfiguration} requires a {expectedConfiguration} engine; the bundled engine is {info.BuildConfiguration}.");
        }
        return info;
    }

    private static IntPtr ResolveSproutLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "sprout_ffi", StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }
        string path = Path.Combine(AppContext.BaseDirectory, "sprout_ffi.dll");
        if (!File.Exists(path))
        {
            throw new DllNotFoundException(
                $"The bundled Sprout engine was not found at `{path}`.");
        }
        return NativeLibrary.Load(path);
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

    public static BuildResponse ParseBuildResponse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        BuildResponse response = JsonSerializer.Deserialize<BuildResponse>(json, JsonOptions)
            ?? throw new InvalidDataException("The Sprout build service returned an empty response.");
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

    private static ProjectResponse UpdateDependency(
        string projectPath,
        ProjectDependencyKind kind,
        string name,
        string? requirement)
    {
        byte[] path = Encoding.UTF8.GetBytes(projectPath);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] requirementBytes = Encoding.UTF8.GetBytes(requirement ?? string.Empty);
        uint operation = requirement is null ? 1u : 0u;
        NativeBuffer buffer = NativeMethods.SproutProjectDependency(
            (uint)kind,
            operation,
            path,
            (nuint)path.Length,
            nameBytes,
            (nuint)nameBytes.Length,
            requirementBytes,
            (nuint)requirementBytes.Length);
        return ParseProjectResponse(ReadBuffer(buffer));
    }

    private static BuildResponse BuildProject(string projectPath)
    {
        byte[] path = Encoding.UTF8.GetBytes(projectPath);
        NativeBuffer buffer = NativeMethods.SproutProjectBuild(path, (nuint)path.Length);
        return ParseBuildResponse(ReadBuffer(buffer));
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

        [LibraryImport(LibraryName, EntryPoint = "sprout_engine_info")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutEngineInfo();

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

        [LibraryImport(LibraryName, EntryPoint = "sprout_project_dependency")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutProjectDependency(
            uint kind,
            uint operation,
            byte[] projectPath,
            nuint projectPathLength,
            byte[] name,
            nuint nameLength,
            byte[] requirement,
            nuint requirementLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_project_build")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutProjectBuild(
            byte[] projectPath,
            nuint projectPathLength);

        [LibraryImport(LibraryName, EntryPoint = "sprout_get_ast_json", StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial NativeBuffer SproutGetAstJson(string source);

        [LibraryImport(LibraryName, EntryPoint = "sprout_buffer_free")]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void SproutBufferFree(NativeBuffer buffer);
    }
}
