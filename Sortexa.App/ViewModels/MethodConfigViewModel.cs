using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sortexa.App.Services;
using Sortexa.Core.Interfaces;
using Sortexa.Core.Models;

namespace Sortexa.App.ViewModels;

public sealed class MethodConfigViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly Func<MethodSelectionViewModel> _methodSelectionFactory;
    private readonly Func<PreviewViewModel> _previewFactory;
    private readonly IOrganizationPlanner _planner;
    private readonly IClassificationRegistry _classificationRegistry;
    private readonly ISettingsRepository _settingsRepository;

    private string _sourceDirectory = string.Empty;
    private IReadOnlyList<FileItem> _files = Array.Empty<FileItem>();
    private OrganizationMethod _method;

    // Config options
    private DateGroupingFormat _dateFormat = DateGroupingFormat.YearMonth;
    private int _extensionThreshold = 0;
    private bool _groupExtensionless = true;
    private string _fallbackFolder = "Other Files";

    // Custom Rules state
    private string _newRuleName = "Keyword Rule";
    private RuleConditionField _newRuleField = RuleConditionField.FileNameContains;
    private RuleConditionOperator _newRuleOperator = RuleConditionOperator.Contains;
    private string _newRuleValue = "";
    private string _newRuleTargetFolder = "Matched";
    private int _newRulePriority = 10;

    public MethodConfigViewModel(
        INavigationService navigationService,
        Func<MethodSelectionViewModel> methodSelectionFactory,
        Func<PreviewViewModel> previewFactory,
        IOrganizationPlanner planner,
        IClassificationRegistry classificationRegistry,
        ISettingsRepository settingsRepository)
    {
        _navigationService = navigationService;
        _methodSelectionFactory = methodSelectionFactory;
        _previewFactory = previewFactory;
        _planner = planner;
        _classificationRegistry = classificationRegistry;
        _settingsRepository = settingsRepository;

        CustomRules = new ObservableCollection<CustomRule>();

        BackCommand = new RelayCommand(GoBack);
        ContinueCommand = new RelayCommand(GeneratePlanAndProceed);
        AddRuleCommand = new RelayCommand(AddCustomRule, _ => !string.IsNullOrWhiteSpace(NewRuleValue) && !string.IsNullOrWhiteSpace(NewRuleTargetFolder));
        RemoveRuleCommand = new RelayCommand(p =>
        {
            if (p is CustomRule rule)
            {
                CustomRules.Remove(rule);
            }
        });
    }

    public ObservableCollection<CustomRule> CustomRules { get; }

    public OrganizationMethod Method
    {
        get => _method;
        private set
        {
            if (SetProperty(ref _method, value))
            {
                OnPropertyChanged(nameof(MethodTitle));
                OnPropertyChanged(nameof(MethodDescription));
                OnPropertyChanged(nameof(IsDateConfigVisible));
                OnPropertyChanged(nameof(IsExtensionConfigVisible));
                OnPropertyChanged(nameof(IsCustomRulesConfigVisible));
                OnPropertyChanged(nameof(IsStandardConfigVisible));
            }
        }
    }

    public string MethodTitle => Method switch
    {
        OrganizationMethod.SmartCategories => "Smart Categories Configuration",
        OrganizationMethod.FileExtensions => "File Extensions Configuration",
        OrganizationMethod.DateCreated => "Date Created Configuration",
        OrganizationMethod.LastModified => "Last Modified Configuration",
        OrganizationMethod.FileSize => "File Size Configuration",
        OrganizationMethod.FilePurpose => "File Purpose Configuration",
        OrganizationMethod.CustomRules => "Custom Rules Configuration",
        _ => "Method Configuration"
    };

    public string MethodDescription => Method switch
    {
        OrganizationMethod.SmartCategories => "Files will be automatically sorted into categories based on your extension registry. You can customize category mappings in the Categories screen.",
        OrganizationMethod.FileExtensions => "Files will be placed into folders named after their uppercase extension. Configure grouping for rare extensions or extensionless files below.",
        OrganizationMethod.DateCreated => "Files will be sorted according to their filesystem creation timestamp. Note that copied or newly downloaded files may carry recent creation dates.",
        OrganizationMethod.LastModified => "Files will be grouped by their latest modification timestamp.",
        OrganizationMethod.FileSize => "Files will be organized into standardized size tiers: Tiny (<100 KB), Small (100 KB - 1 MB), Medium (1 MB - 100 MB), Large (100 MB - 1 GB), Very Large (>=1 GB).",
        OrganizationMethod.FilePurpose => "Files will be grouped by functional purpose: Applications & Installers, Documents & Office, Media, Development, Archives, and Creative Assets.",
        OrganizationMethod.CustomRules => "Define tailored pattern-matching rules with custom target subfolders.",
        _ => "Configure your organization parameters."
    };

    public bool IsDateConfigVisible => Method == OrganizationMethod.DateCreated || Method == OrganizationMethod.LastModified;
    public bool IsExtensionConfigVisible => Method == OrganizationMethod.FileExtensions;
    public bool IsCustomRulesConfigVisible => Method == OrganizationMethod.CustomRules;
    public bool IsStandardConfigVisible => !IsDateConfigVisible && !IsExtensionConfigVisible && !IsCustomRulesConfigVisible;

    public DateGroupingFormat DateFormat
    {
        get => _dateFormat;
        set => SetProperty(ref _dateFormat, value);
    }

    public int ExtensionThreshold
    {
        get => _extensionThreshold;
        set => SetProperty(ref _extensionThreshold, value);
    }

    public bool GroupExtensionless
    {
        get => _groupExtensionless;
        set => SetProperty(ref _groupExtensionless, value);
    }

    public string FallbackFolder
    {
        get => _fallbackFolder;
        set => SetProperty(ref _fallbackFolder, value);
    }

    // New rule fields
    public string NewRuleName
    {
        get => _newRuleName;
        set => SetProperty(ref _newRuleName, value);
    }

    public RuleConditionField NewRuleField
    {
        get => _newRuleField;
        set => SetProperty(ref _newRuleField, value);
    }

    public RuleConditionOperator NewRuleOperator
    {
        get => _newRuleOperator;
        set => SetProperty(ref _newRuleOperator, value);
    }

    public string NewRuleValue
    {
        get => _newRuleValue;
        set => SetProperty(ref _newRuleValue, value);
    }

    public string NewRuleTargetFolder
    {
        get => _newRuleTargetFolder;
        set => SetProperty(ref _newRuleTargetFolder, value);
    }

    public int NewRulePriority
    {
        get => _newRulePriority;
        set => SetProperty(ref _newRulePriority, value);
    }

    public ICommand BackCommand { get; }
    public ICommand ContinueCommand { get; }
    public ICommand AddRuleCommand { get; }
    public ICommand RemoveRuleCommand { get; }

    public void Initialize(string sourceDirectory, IReadOnlyList<FileItem> files, OrganizationMethod method)
    {
        _sourceDirectory = sourceDirectory;
        _files = files;
        Method = method;

        // Load existing custom rules from settings if CustomRules method
        if (Method == OrganizationMethod.CustomRules && CustomRules.Count == 0)
        {
            CustomRules.Add(new CustomRule
            {
                RuleName = "Invoices & Receipts",
                Field = RuleConditionField.FileNameContains,
                Operator = RuleConditionOperator.Contains,
                Value = "Invoice",
                TargetSubfolder = "Invoices",
                Priority = 1
            });
            CustomRules.Add(new CustomRule
            {
                RuleName = "Screenshots",
                Field = RuleConditionField.FileNamePrefix,
                Operator = RuleConditionOperator.StartsWith,
                Value = "Screenshot",
                TargetSubfolder = "Screenshots",
                Priority = 2
            });
        }
    }

    private void AddCustomRule()
    {
        if (string.IsNullOrWhiteSpace(NewRuleValue) || string.IsNullOrWhiteSpace(NewRuleTargetFolder))
            return;

        CustomRules.Add(new CustomRule
        {
            RuleName = string.IsNullOrWhiteSpace(NewRuleName) ? "Custom Rule" : NewRuleName,
            Field = NewRuleField,
            Operator = NewRuleOperator,
            Value = NewRuleValue.Trim(),
            TargetSubfolder = NewRuleTargetFolder.Trim(),
            Priority = NewRulePriority
        });

        NewRuleValue = string.Empty;
        NewRuleName = "New Rule";
    }

    private void GoBack()
    {
        var selectionVm = _methodSelectionFactory();
        selectionVm.Initialize(_sourceDirectory, _files);
        _navigationService.NavigateTo(selectionVm);
    }

    private void GeneratePlanAndProceed()
    {
        var options = new MethodOptions
        {
            DateFormat = DateFormat,
            ExtensionMinimumCountThreshold = ExtensionThreshold,
            GroupExtensionlessAsNoExtension = GroupExtensionless,
            FallbackFolder = string.IsNullOrWhiteSpace(FallbackFolder) ? "Other Files" : FallbackFolder.Trim()
        };

        var plan = _planner.CreatePlan(
            _sourceDirectory,
            _files,
            Method,
            options,
            _classificationRegistry,
            CustomRules);

        var previewVm = _previewFactory();
        previewVm.Initialize(plan);
        _navigationService.NavigateTo(previewVm);
    }
}
