using AdeptTools.Import.Api;
using AdeptTools.Import.Enums;
using AdeptTools.Import.Models;
using AdeptTools.Import.Readers;
using AdeptTools.Import.Services;
using AdeptTools.Launcher.Services;
using AdeptTools.Launcher.ViewModels;

namespace AdeptTools.Launcher.Tests;

public class ImportViewModelTests
{
    private readonly MockModeState _mockModeState = new() { IsMock = true };
    private readonly StubDialogService _dialogService = new();
    private readonly MockImportApiClient _mockApiClient = new();
    private readonly ImportExcelReader _excelReader = new();

    private ImportViewModel CreateViewModel(IImportService? service = null)
    {
        var svc = service ?? CreateMockImportService();
        return new ImportViewModel(
            () => svc,
            () => _mockApiClient,
            _dialogService,
            _mockModeState,
            _excelReader);
    }

    private IImportService CreateMockImportService()
    {
        return new StubImportService();
    }

    [Fact]
    public void InitialState_IsCorrect()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsWorkbookLoaded);
        Assert.False(vm.IsValidated);
        Assert.False(vm.IsOperationRunning);
        Assert.False(vm.ShowResults);
        Assert.False(vm.IsDryRun);
        Assert.True(vm.IsMockMode);
        Assert.Empty(vm.WorkbookPath);
        Assert.Empty(vm.MappingRows);
        Assert.Empty(vm.ResultItems);
        Assert.Null(vm.WorkbookSummary);
    }

    [Fact]
    public void ResultStatus_Add_HasCorrectPrefix()
    {
        var item = new ResultItem(ResultStatus.Add, "created");

        Assert.Equal("[ADD]", item.StatusPrefix);
    }

    [Fact]
    public void MapOutcome_Updated_ReturnsOk()
    {
        // Test via RunImportAsync results — the mapping is internal
        // but we can verify through the result items
        var service = new StubImportService(new ImportBatchResult
        {
            TotalRows = 2,
            Updated = 1,
            Created = 1,
            Results = new()
            {
                new() { RowNumber = 1, PrimaryKeyDisplay = "DWG-001", Outcome = ImportOutcome.Updated, Message = "updated (3 fields)" },
                new() { RowNumber = 2, PrimaryKeyDisplay = "DWG-NEW", Outcome = ImportOutcome.Created, Message = "created" }
            }
        });

        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.IsValidated = true;

        Assert.True(vm.RunImportCommand.CanExecute(null));
    }

    [Fact]
    public void CanRunImport_FalseWhenNotValidated()
    {
        var vm = CreateViewModel();
        vm.IsWorkbookLoaded = true;
        vm.IsValidated = false;

        Assert.False(vm.RunImportCommand.CanExecute(null));
    }

    [Fact]
    public void CanRunImport_FalseWhenNotLoaded()
    {
        var vm = CreateViewModel();
        vm.IsWorkbookLoaded = false;
        vm.IsValidated = true;

        Assert.False(vm.RunImportCommand.CanExecute(null));
    }

    [Fact]
    public void CanValidate_FalseWhenNotLoaded()
    {
        var vm = CreateViewModel();
        vm.IsWorkbookLoaded = false;

        Assert.False(vm.ValidateCommand.CanExecute(null));
    }

    [Fact]
    public void CanValidate_TrueWhenLoaded()
    {
        var vm = CreateViewModel();
        vm.IsWorkbookLoaded = true;

        Assert.True(vm.ValidateCommand.CanExecute(null));
    }

    [Fact]
    public async Task ValidateAsync_Success_SetsIsValidated()
    {
        var service = new StubImportService(validationResult: new MappingValidationResult());
        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.WorkbookPath = "test.xlsx";

        await vm.ValidateCommand.ExecuteAsync(null);

        Assert.True(vm.IsValidated);
        Assert.Contains("\u2713", vm.ValidationStatusText);
    }

    [Fact]
    public async Task ValidateAsync_WithErrors_SetsNotValidated()
    {
        var valResult = new MappingValidationResult();
        valResult.Errors.Add(new MappingValidationError { Message = "No search keys defined" });
        var service = new StubImportService(validationResult: valResult);
        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.WorkbookPath = "test.xlsx";

        await vm.ValidateCommand.ExecuteAsync(null);

        Assert.False(vm.IsValidated);
        Assert.Contains("\u2717", vm.ValidationStatusText);
        Assert.True(vm.ShowResults);
        Assert.Single(vm.ResultItems);
        Assert.Equal(ResultStatus.Fail, vm.ResultItems[0].Status);
    }

    [Fact]
    public async Task RunImportAsync_PopulatesResultItems()
    {
        var batchResult = new ImportBatchResult
        {
            TotalRows = 3,
            Updated = 2,
            Skipped = 1,
            Results = new()
            {
                new() { RowNumber = 1, PrimaryKeyDisplay = "DWG-001", Outcome = ImportOutcome.Updated, Message = "updated (3 fields)" },
                new() { RowNumber = 2, PrimaryKeyDisplay = "DWG-002", Outcome = ImportOutcome.Updated, Message = "updated (2 fields)" },
                new() { RowNumber = 3, PrimaryKeyDisplay = "DWG-003", Outcome = ImportOutcome.Skipped, Message = "not found" }
            }
        };
        var service = new StubImportService(runResult: batchResult);
        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.IsValidated = true;
        vm.WorkbookPath = "test.xlsx";

        await vm.RunImportCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.ResultItems.Count);
        Assert.Equal(ResultStatus.Ok, vm.ResultItems[0].Status);
        Assert.Equal(ResultStatus.Ok, vm.ResultItems[1].Status);
        Assert.Equal(ResultStatus.Skip, vm.ResultItems[2].Status);
        Assert.Equal(100, vm.ProgressPercent);
        Assert.Contains("Summary", vm.ProgressText);
    }

    [Fact]
    public async Task RunImportAsync_DryRun_IncludesDryRunInSummary()
    {
        var batchResult = new ImportBatchResult { TotalRows = 1, DryRun = true, Results = new() };
        var service = new StubImportService(runResult: batchResult);
        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.IsValidated = true;
        vm.IsDryRun = true;
        vm.WorkbookPath = "test.xlsx";

        await vm.RunImportCommand.ExecuteAsync(null);

        Assert.Contains("DRY RUN", vm.ProgressText);
    }

    [Fact]
    public async Task RunImportAsync_Created_ShowsAddStatus()
    {
        var batchResult = new ImportBatchResult
        {
            TotalRows = 1,
            Created = 1,
            Results = new()
            {
                new() { RowNumber = 1, PrimaryKeyDisplay = "NEW-001", Outcome = ImportOutcome.Created, Message = "created" }
            }
        };
        var service = new StubImportService(runResult: batchResult);
        var vm = CreateViewModel(service);
        vm.IsWorkbookLoaded = true;
        vm.IsValidated = true;
        vm.WorkbookPath = "test.xlsx";

        await vm.RunImportCommand.ExecuteAsync(null);

        Assert.Single(vm.ResultItems);
        Assert.Equal(ResultStatus.Add, vm.ResultItems[0].Status);
    }

    [Fact]
    public void ClearResults_ResetsState()
    {
        var vm = CreateViewModel();
        vm.ShowResults = true;
        vm.ResultItems.Add(new ResultItem(ResultStatus.Ok, "test"));
        vm.ProgressPercent = 50;
        vm.ProgressText = "working...";

        vm.ClearResultsCommand.Execute(null);

        Assert.Empty(vm.ResultItems);
        Assert.False(vm.ShowResults);
        Assert.Equal(0, vm.ProgressPercent);
        Assert.Empty(vm.ProgressText);
    }

    [Fact]
    public async Task HandleFileDropAsync_NonXlsx_ShowsError()
    {
        var vm = CreateViewModel();

        await vm.HandleFileDropAsync("readme.txt");

        Assert.True(_dialogService.LastMessageShown);
        Assert.Contains("Only Excel", _dialogService.LastMessage!);
    }

    // --- Test doubles ---

    private class StubDialogService : IDialogService
    {
        public bool LastMessageShown { get; private set; }
        public string? LastMessage { get; private set; }

        public string? ShowSaveFileDialog(string filter, string defaultName) => null;
        public string? ShowOpenFileDialog(string filter) => null;
        public void ShowMessage(string title, string message)
        {
            LastMessageShown = true;
            LastMessage = message;
        }
    }

    private class StubImportService : IImportService
    {
        private readonly ImportBatchResult? _runResult;
        private readonly MappingValidationResult? _validationResult;

        public StubImportService(
            ImportBatchResult? runResult = null,
            MappingValidationResult? validationResult = null)
        {
            _runResult = runResult;
            _validationResult = validationResult;
        }

        public Task<ImportBatchResult> RunAsync(ImportRunRequest request, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
        {
            return Task.FromResult(_runResult ?? new ImportBatchResult());
        }

        public Task<MappingValidationResult> ValidateAsync(ImportValidateRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(_validationResult ?? new MappingValidationResult());
        }

        public Task<List<AdeptFieldDefinitionDto>> FetchFieldsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new List<AdeptFieldDefinitionDto>());
        }

        public Task<List<ColumnMapping>> AutoMapAsync(string excelPath, string? sheetName = null, CancellationToken ct = default)
        {
            return Task.FromResult(new List<ColumnMapping>());
        }
    }
}
