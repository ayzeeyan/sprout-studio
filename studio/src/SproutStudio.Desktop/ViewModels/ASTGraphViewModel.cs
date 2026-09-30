using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SproutStudio.Core;

namespace SproutStudio.ViewModels;

public enum AstNodeCategory
{
    All,
    Statements,
    Expressions,
    Functions,
    Imports,
}

public sealed class AstNodeViewModel : ObservableObject
{
    private bool _isExpanded = true;
    private bool _isSelected;
    private bool _isVisible = true;

    public AstNodeViewModel(
        string kind,
        string label,
        string detail,
        SourceSpan span,
        int depth,
        AstNodeViewModel? parent = null)
    {
        Kind = kind;
        Label = label;
        Detail = detail;
        Span = span;
        Depth = depth;
        Parent = parent;
        Category = DetermineCategory(kind);
        (BadgeColor, BadgeBackground, Icon) = DetermineVisuals(kind, Category);
    }

    public string Kind { get; }

    public string Label { get; }

    public string Detail { get; }

    public SourceSpan Span { get; }

    public int Depth { get; }

    public AstNodeViewModel? Parent { get; }

    public AstNodeCategory Category { get; }

    public string BadgeColor { get; }

    public string BadgeBackground { get; }

    public string Icon { get; }

    public ObservableCollection<AstNodeViewModel> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public string LocationText => $"Line {Span.StartLine}:{Span.StartColumn} - {Span.EndLine}:{Span.EndColumn}";

    public void ExpandAll()
    {
        IsExpanded = true;
        foreach (AstNodeViewModel child in Children)
        {
            child.ExpandAll();
        }
    }

    public void CollapseAll()
    {
        IsExpanded = false;
        foreach (AstNodeViewModel child in Children)
        {
            child.CollapseAll();
        }
    }

    public void ApplyFilter(AstNodeCategory category, string query)
    {
        bool matchesCategory = category == AstNodeCategory.All
            || Category == category
            || Kind == "Program";

        bool matchesQuery = string.IsNullOrWhiteSpace(query)
            || Label.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Kind.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Detail.Contains(query, StringComparison.OrdinalIgnoreCase);

        bool selfMatches = matchesCategory && matchesQuery;
        bool anyChildVisible = false;

        foreach (AstNodeViewModel child in Children)
        {
            child.ApplyFilter(category, query);
            if (child.IsVisible)
            {
                anyChildVisible = true;
            }
        }

        IsVisible = selfMatches || anyChildVisible;
        if (anyChildVisible && !string.IsNullOrWhiteSpace(query))
        {
            IsExpanded = true;
        }
    }

    private static AstNodeCategory DetermineCategory(string kind) => kind switch
    {
        "Import" or "ImportPython" => AstNodeCategory.Imports,
        "Function" or "Class" => AstNodeCategory.Functions,
        "Variable" or "Assignment" or "If" or "Repeat" or "While" or "Return" or "ProfileBlock" or "ExpressionStatement" => AstNodeCategory.Statements,
        "Integer" or "Float" or "Bool" or "String" or "Nil" or "VariableIdentifier" or "Unary" or "Binary" or "Call" or "Member" or "Index" => AstNodeCategory.Expressions,
        _ => AstNodeCategory.Statements,
    };

    private static (string Color, string Background, string Icon) DetermineVisuals(string kind, AstNodeCategory category) => kind switch
    {
        "Program" => ("#60A5FA", "#1E293B", "◈"),
        "Import" or "ImportPython" => ("#34D399", "#064E3B", "⇲"),
        "Function" => ("#F59E0B", "#78350F", "λ"),
        "Class" => ("#FBBF24", "#78350F", "❖"),
        "Variable" => ("#38BDF8", "#0C4A6E", "⬡"),
        "Assignment" => ("#38BDF8", "#0C4A6E", "←"),
        "If" => ("#EC4899", "#831843", "⌥"),
        "Repeat" or "While" => ("#F472B6", "#831843", "↻"),
        "Return" => ("#A78BFA", "#4C1D95", "↵"),
        "ProfileBlock" => ("#6EE7B7", "#064E3B", "§"),
        "Call" => ("#C084FC", "#581C87", "▶"),
        "Binary" or "Unary" => ("#A855F7", "#3B0764", "⚙"),
        "Integer" or "Float" or "Bool" or "String" or "Nil" => ("#94A3B8", "#1E293B", "•"),
        "Member" or "Index" => ("#CBD5E1", "#334155", "·"),
        _ => category switch
        {
            AstNodeCategory.Imports => ("#34D399", "#064E3B", "⇲"),
            AstNodeCategory.Functions => ("#F59E0B", "#78350F", "λ"),
            AstNodeCategory.Statements => ("#38BDF8", "#0C4A6E", "▪"),
            AstNodeCategory.Expressions => ("#A855F7", "#3B0764", "•"),
            _ => ("#94A3B8", "#1E293B", "▫"),
        },
    };
}

public sealed partial class ASTGraphViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private AstNodeViewModel? _selectedNode;
    private string _searchText = string.Empty;
    private AstNodeCategory _selectedCategory = AstNodeCategory.All;
    private bool _isVisualGraphView;
    private int _totalNodeCount;
    private int _statementCount;
    private int _expressionCount;
    private int _functionCount;
    private int _importCount;
    private int _maxDepth;

    public ASTGraphViewModel()
    {
        ExpandAllCommand = new RelayCommand(ExpandAll);
        CollapseAllCommand = new RelayCommand(CollapseAll);
        SelectCategoryCommand = new RelayCommand<AstNodeCategory>(SetCategory);
        ToggleViewModeCommand = new RelayCommand(() => IsVisualGraphView = !IsVisualGraphView);
    }

    public event EventHandler<SourceSpan>? NodeSelected;

    public ObservableCollection<AstNodeViewModel> RootNodes { get; } = [];

    public IRelayCommand ExpandAllCommand { get; }

    public IRelayCommand CollapseAllCommand { get; }

    public IRelayCommand<AstNodeCategory> SelectCategoryCommand { get; }

    public IRelayCommand ToggleViewModeCommand { get; }

    public AstNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetProperty(ref _selectedNode, value) && value is not null)
            {
                NodeSelected?.Invoke(this, value.Span);
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public AstNodeCategory SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(IsStatementsSelected));
                OnPropertyChanged(nameof(IsExpressionsSelected));
                OnPropertyChanged(nameof(IsFunctionsSelected));
                OnPropertyChanged(nameof(IsImportsSelected));
            }
        }
    }

    public bool IsAllSelected => SelectedCategory == AstNodeCategory.All;
    public bool IsStatementsSelected => SelectedCategory == AstNodeCategory.Statements;
    public bool IsExpressionsSelected => SelectedCategory == AstNodeCategory.Expressions;
    public bool IsFunctionsSelected => SelectedCategory == AstNodeCategory.Functions;
    public bool IsImportsSelected => SelectedCategory == AstNodeCategory.Imports;

    public bool IsVisualGraphView
    {
        get => _isVisualGraphView;
        set
        {
            if (SetProperty(ref _isVisualGraphView, value))
            {
                OnPropertyChanged(nameof(ViewModeLabel));
            }
        }
    }

    public string ViewModeLabel => IsVisualGraphView ? "Tree View" : "Visual Canvas";

    public int TotalNodeCount
    {
        get => _totalNodeCount;
        private set
        {
            if (SetProperty(ref _totalNodeCount, value))
            {
                OnPropertyChanged(nameof(HasAst));
                OnPropertyChanged(nameof(NoAst));
            }
        }
    }

    public int StatementCount
    {
        get => _statementCount;
        private set => SetProperty(ref _statementCount, value);
    }

    public int ExpressionCount
    {
        get => _expressionCount;
        private set => SetProperty(ref _expressionCount, value);
    }

    public int FunctionCount
    {
        get => _functionCount;
        private set => SetProperty(ref _functionCount, value);
    }

    public int ImportCount
    {
        get => _importCount;
        private set => SetProperty(ref _importCount, value);
    }

    public int MaxDepth
    {
        get => _maxDepth;
        private set => SetProperty(ref _maxDepth, value);
    }

    public bool HasAst => TotalNodeCount > 0;

    public bool NoAst => TotalNodeCount == 0;

    public void SetCategory(AstNodeCategory category) => SelectedCategory = category;

    public void ExpandAll()
    {
        foreach (AstNodeViewModel root in RootNodes)
        {
            root.ExpandAll();
        }
    }

    public void CollapseAll()
    {
        foreach (AstNodeViewModel root in RootNodes)
        {
            root.CollapseAll();
        }
    }

    public void UpdateAst(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            ClearAst();
            return;
        }

        try
        {
            AstResponse? response = JsonSerializer.Deserialize<AstResponse>(json, JsonOpts);
            if (response?.Program is null)
            {
                ClearAst();
                return;
            }

            int totalNodes = 0;
            int stmts = 0;
            int exprs = 0;
            int fns = 0;
            int imps = 0;
            int maxDepth = 0;

            AstNodeViewModel rootVm = ConvertNode(response.Program, 0, null, ref totalNodes, ref stmts, ref exprs, ref fns, ref imps, ref maxDepth);

            RootNodes.Clear();
            RootNodes.Add(rootVm);

            TotalNodeCount = totalNodes;
            StatementCount = stmts;
            ExpressionCount = exprs;
            FunctionCount = fns;
            ImportCount = imps;
            MaxDepth = maxDepth;

            ApplyFilter();
        }
        catch (Exception)
        {
            ClearAst();
        }
    }

    public void ClearAst()
    {
        RootNodes.Clear();
        SelectedNode = null;
        TotalNodeCount = 0;
        StatementCount = 0;
        ExpressionCount = 0;
        FunctionCount = 0;
        ImportCount = 0;
        MaxDepth = 0;
    }

    private void ApplyFilter()
    {
        foreach (AstNodeViewModel root in RootNodes)
        {
            root.ApplyFilter(SelectedCategory, SearchText);
        }
    }

    private static AstNodeViewModel ConvertNode(
        AstNode node,
        int depth,
        AstNodeViewModel? parent,
        ref int totalNodes,
        ref int stmts,
        ref int exprs,
        ref int fns,
        ref int imps,
        ref int maxDepth)
    {
        totalNodes++;
        if (depth > maxDepth)
        {
            maxDepth = depth;
        }

        var vm = new AstNodeViewModel(node.Kind, node.Label, node.Detail, node.Span, depth, parent);

        switch (vm.Category)
        {
            case AstNodeCategory.Statements:
                stmts++;
                break;
            case AstNodeCategory.Expressions:
                exprs++;
                break;
            case AstNodeCategory.Functions:
                fns++;
                break;
            case AstNodeCategory.Imports:
                imps++;
                break;
        }

        if (node.Children is not null)
        {
            foreach (AstNode child in node.Children)
            {
                AstNodeViewModel childVm = ConvertNode(child, depth + 1, vm, ref totalNodes, ref stmts, ref exprs, ref fns, ref imps, ref maxDepth);
                vm.Children.Add(childVm);
            }
        }

        return vm;
    }
}
